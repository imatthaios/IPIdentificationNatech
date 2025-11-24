using System.Diagnostics;
using System.Threading.Channels;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// In-memory channel backed implementation of IBackgroundBatchProcessor.
/// Processes whole batches in parallel with throttled concurrency.
/// </summary>
public class ChannelBackgroundBatchProcessor : BackgroundService, IBackgroundBatchProcessor
{
    private const int MaxParallelism = 8;

    private readonly Channel<BatchWorkItem> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChannelBackgroundBatchProcessor> _log;

    private sealed record BatchWorkItem(Guid BatchId, IReadOnlyList<string> Ips);

    public ChannelBackgroundBatchProcessor(
        IServiceProvider serviceProvider,
        ILogger<ChannelBackgroundBatchProcessor> log)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _channel = Channel.CreateUnbounded<BatchWorkItem>();
    }

    public int? GetApproximateQueueLength()
    {
        return null;
    }

    /// <summary>
    /// Enqueue a whole batch of IPs as a single unit of work.
    /// </summary>
    public Task QueueBatchAsync(IEnumerable<string> ips, Guid batchId, CancellationToken cancellationToken = default)
    {
        var ipList = ips
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ipList.Count == 0)
        {
            _log.LogWarning("No valid IPs to enqueue for batch {BatchId}", batchId);
            return Task.CompletedTask;
        }

        if (!_channel.Writer.TryWrite(new BatchWorkItem(batchId, ipList)))
        {
            _log.LogWarning("Failed to enqueue batch {BatchId}", batchId);
        }
        else
        {
            _log.LogInformation("Enqueued batch {BatchId} with {Count} IPs", batchId, ipList.Count);
        }

        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ChannelBackgroundBatchProcessor started");

        await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(work, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _log.LogInformation("Background processor is stopping due to cancellation.");
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing batch {BatchId}", work.BatchId);
            }
        }

        _log.LogInformation("ChannelBackgroundBatchProcessor stopped");
    }

    private async Task ProcessBatchAsync(BatchWorkItem work, CancellationToken token)
    {
        _log.LogInformation(
            "Starting processing of batch {BatchId} with {Count} IPs (max parallelism: {MaxParallelism})",
            work.BatchId,
            work.Ips.Count,
            MaxParallelism);

        using (var scope = _serviceProvider.CreateScope())
        {
            var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
            var batch = await batchRepo.GetByIdAsync(work.BatchId);

            if (batch == null)
            {
                _log.LogWarning("Batch {BatchId} not found", work.BatchId);
                return;
            }

            if (batch.Status == BatchStatus.Pending)
            {
                batch.Status = BatchStatus.Running;
                batch.StartedAtUtc ??= DateTime.UtcNow;
                await batchRepo.SaveChangesAsync();
            }
        }

        using var semaphore = new SemaphoreSlim(MaxParallelism);

        var tasks = work.Ips.Select(ip => ProcessSingleIpAsync(
            work.BatchId,
            ip,
            semaphore,
            token));

        await Task.WhenAll(tasks);

        using (var scope = _serviceProvider.CreateScope())
        {
            var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
            var batch = await batchRepo.GetByIdAsync(work.BatchId);

            if (batch == null)
            {
                _log.LogWarning("Batch {BatchId} disappeared while recomputing aggregates", work.BatchId);
                return;
            }

            var items = batch.Items;

            var processedItems = items
                .Where(i => i.Status is BatchItemStatus.Succeeded or BatchItemStatus.Failed)
                .ToList();

            batch.ProcessedCount = processedItems.Count;

            var durations = processedItems
                .Where(i => i.DurationMs.HasValue && i.DurationMs.Value > 0)
                .Select(i => (double)i.DurationMs!.Value)
                .ToList();

            batch.AverageMsPerItem = durations.Count > 0
                ? durations.Average()
                : null;

            if (batch.ProcessedCount >= batch.TotalCount)
            {
                batch.Status = BatchStatus.Completed;
                batch.CompletedAtUtc = DateTime.UtcNow;
            }
            else if (batch.Status == BatchStatus.Pending && processedItems.Count > 0)
            {
                batch.Status = BatchStatus.Running;
                batch.StartedAtUtc ??= DateTime.UtcNow;
            }

            await batchRepo.SaveChangesAsync();
        }

        _log.LogInformation("Finished processing batch {BatchId}", work.BatchId);
    }

    private async Task ProcessSingleIpAsync(
        Guid batchId,
        string ip,
        SemaphoreSlim semaphore,
        CancellationToken token)
    {
        await semaphore.WaitAsync(token);
        try
        {
            using var scope = _serviceProvider.CreateScope();

            var geoClient = scope.ServiceProvider.GetRequiredService<IGeoProviderClient>();
            var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
            var cacheRepo = scope.ServiceProvider.GetRequiredService<IGeoCacheRepository>();

            var batch = await batchRepo.GetByIdAsync(batchId);
            if (batch == null)
            {
                _log.LogWarning("Batch {BatchId} not found while processing IP {Ip}", batchId, ip);
                return;
            }

            var item = batch.Items.FirstOrDefault(i =>
                i.Ip == ip && i.Status == BatchItemStatus.Pending);

            if (item == null)
            {
                _log.LogWarning("Batch item not found or already processed for batch {BatchId} and IP {Ip}", batchId, ip);
                return;
            }

            item.Status = BatchItemStatus.Running;
            item.Attempts++;
            item.StartedAtUtc = DateTime.UtcNow;

            var sw = Stopwatch.StartNew();
            var dto = await geoClient.FetchIpInfoAsync(ip, token);
            sw.Stop();

            if (dto == null)
            {
                item.Status = BatchItemStatus.Failed;
                item.ErrorMessage = "Geo provider returned no data.";
            }
            else
            {
                item.Status = BatchItemStatus.Succeeded;
                item.CountryCode = dto.CountryCode;
                item.CountryName = dto.CountryName;
                item.TimeZone = dto.TimeZone;
                item.Latitude = dto.Latitude;
                item.Longitude = dto.Longitude;

                await cacheRepo.AddOrUpdateAsync(new IpGeoCache
                {
                    Ip = dto.Ip,
                    CountryCode = dto.CountryCode,
                    CountryName = dto.CountryName,
                    TimeZone = dto.TimeZone,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    LastFetchedUtc = DateTime.UtcNow
                });

                await cacheRepo.SaveChangesAsync();
            }

            item.CompletedAtUtc = DateTime.UtcNow;
            item.DurationMs = (int)sw.ElapsedMilliseconds;

            await batchRepo.SaveChangesAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _log.LogInformation("Processing cancelled for IP {Ip} in batch {BatchId}", ip, batchId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing IP {Ip} for batch {BatchId}", ip, batchId);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
