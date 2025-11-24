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
/// </summary>
public class ChannelBackgroundBatchProcessor : BackgroundService, IBackgroundBatchProcessor
{
    private readonly Channel<(Guid BatchId, string Ip)> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChannelBackgroundBatchProcessor> _log;

    public ChannelBackgroundBatchProcessor(
        IServiceProvider serviceProvider,
        ILogger<ChannelBackgroundBatchProcessor> log)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _channel = Channel.CreateUnbounded<(Guid, string)>();
    }

    public int? GetApproximateQueueLength()
    {
        return null;
    }

    public Task QueueBatchAsync(IEnumerable<string> ips, Guid batchId, CancellationToken cancellationToken = default)
    {
        foreach (var ip in ips)
        {
            if (!_channel.Writer.TryWrite((batchId, ip)))
            {
                // await _channel.Writer.WriteAsync((batchId, ip), cancellationToken);
                _log.LogWarning("Failed to enqueue IP {Ip} for batch {BatchId}", ip, batchId);
            }
        }

        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ChannelBackgroundBatchProcessor started");

        await foreach (var (batchId, ip) in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var geoClient = scope.ServiceProvider.GetRequiredService<IGeoProviderClient>();
                var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
                var itemRepo = scope.ServiceProvider.GetRequiredService<IBatchItemRepository>();
                var cacheRepo = scope.ServiceProvider.GetRequiredService<IGeoCacheRepository>();

                var batch = await batchRepo.GetByIdAsync(batchId);
                if (batch == null)
                {
                    _log.LogWarning("Batch {BatchId} not found while processing IP {Ip}", batchId, ip);
                    continue;
                }

                var item = batch.Items.FirstOrDefault(i => i.Ip == ip && i.Status == BatchItemStatus.Pending);
                if (item == null)
                {
                    _log.LogWarning("Batch item not found or already processed for batch {BatchId} and IP {Ip}", batchId, ip);
                    continue;
                }

                item.Status = BatchItemStatus.Running;
                item.Attempts++;
                item.StartedAtUtc = DateTime.UtcNow;

                var sw = Stopwatch.StartNew();
                var dto = await geoClient.FetchIpInfoAsync(ip, stoppingToken);
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

                batch.ProcessedCount++;

                if (item.DurationMs > 0)
                {
                    var duration = (double)item.DurationMs.Value;
                    if (batch is { AverageMsPerItem: not null, ProcessedCount: > 1 })
                    {
                        var previous = batch.AverageMsPerItem.Value;
                        batch.AverageMsPerItem =
                            ((previous * (batch.ProcessedCount - 1)) + duration) / batch.ProcessedCount;
                    }
                    else
                    {
                        batch.AverageMsPerItem = duration;
                    }
                }

                if (batch.ProcessedCount >= batch.TotalCount)
                {
                    batch.Status = BatchStatus.Completed;
                    batch.CompletedAtUtc = DateTime.UtcNow;
                }
                else if (batch.Status == BatchStatus.Pending)
                {
                    batch.Status = BatchStatus.Running;
                    batch.StartedAtUtc ??= DateTime.UtcNow;
                }

                await batchRepo.SaveChangesAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _log.LogInformation("Background processor is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing IP {Ip} for batch {BatchId}", ip, batchId);
            }
        }

        _log.LogInformation("ChannelBackgroundBatchProcessor stopped");
    }
}
