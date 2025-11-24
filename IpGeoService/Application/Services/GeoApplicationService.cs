using System.Net;
using Application.Common;
using Application.Dtos;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class GeoApplicationService : IGeoApplicationService
{
    private readonly IBatchRepository _batchRepo;
    private readonly IBatchItemRepository _batchItemRepo;
    private readonly IGeoCacheRepository _cacheRepo;
    private readonly IGeoProviderClient _geoProvider;
    private readonly IBackgroundBatchProcessor _batchProcessor;
    private readonly ILogger<GeoApplicationService> _log;

    private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(12);

    public GeoApplicationService(
        IBatchRepository batchRepo,
        IBatchItemRepository batchItemRepo,
        IGeoCacheRepository cacheRepo,
        IGeoProviderClient geoProvider,
        IBackgroundBatchProcessor batchProcessor,
        ILogger<GeoApplicationService> log)
    {
        _batchRepo = batchRepo;
        _batchItemRepo = batchItemRepo;
        _cacheRepo = cacheRepo;
        _geoProvider = geoProvider;
        _batchProcessor = batchProcessor;
        _log = log;
    }

    public async Task<Result<IpGeoDto>> GetGeoForIpAsync(string ip)
    {
        var validation = ValidateIp(ip);
        if (!validation.IsSuccess) return Result<IpGeoDto>.Failure(validation.Error);

        var normalizedIp = validation.Value;

        var cached = await TryGetFromCacheAsync(normalizedIp);
        if (cached != null)
            return Result<IpGeoDto>.Success(cached);

        var fetched = await FetchFromProviderAndUpdateCacheAsync(normalizedIp);

        return fetched == null
            ? Result<IpGeoDto>.Failure("Geo provider did not return data.", ErrorType.Unexpected)
            : Result<IpGeoDto>.Success(fetched);
    }
    
    public async Task<Result<BatchAcceptedDto>> EnqueueBatchAsync(string[] ips)
    {
        var validation = ValidateAndNormalizeIps(ips);
        if (!validation.IsSuccess)
            return Result<BatchAcceptedDto>.Failure(validation.Error);

        var normalizedIps = validation.Value;

        try
        {
            var batch = await CreateAndPersistBatchAsync(normalizedIps);

            await _batchProcessor.QueueBatchAsync(normalizedIps, batch.Id);

            var dto = new BatchAcceptedDto
            {
                BatchId = batch.Id,
                StatusUrl = string.Empty
            };

            return Result<BatchAcceptedDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error enqueuing batch");
            return Result<BatchAcceptedDto>.Failure(
                "Internal server error while enqueuing batch.",
                ErrorType.Unexpected);
        }
    }

    public async Task<Result<BatchStatusDto>> GetBatchStatusAsync(Guid batchId)
    {
        var idValidation = ValidateBatchId(batchId);
        if (!idValidation.IsSuccess)
            return Result<BatchStatusDto>.Failure(idValidation.Error);

        try
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                return Result<BatchStatusDto>.Failure("Batch not found.", ErrorType.NotFound);

            var dto = MapToBatchStatusDto(batch);
            
            return Result<BatchStatusDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error reading batch status for {BatchId}", batchId);
            return Result<BatchStatusDto>.Failure(
                "Internal server error while retrieving batch status.",
                ErrorType.Unexpected);
        }
    }
    
    private static Result<string> ValidateIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return Result<string>.Failure("IP address is required.", ErrorType.Validation);

        var trimmed = ip.Trim();

        return !IPAddress.TryParse(trimmed, out _) ?
            Result<string>.Failure("Invalid IP address format.", ErrorType.Validation) :
            Result<string>.Success(trimmed);
    }

    private async Task<IpGeoDto?> TryGetFromCacheAsync(string ip)
    {
        var cache = await _cacheRepo.GetAsync(ip);
        if (cache == null)
            return null;

        if (DateTime.UtcNow - cache.LastFetchedUtc > _cacheTtl)
            return null;

        return new IpGeoDto
        {
            Ip = cache.Ip,
            CountryCode = cache.CountryCode,
            CountryName = cache.CountryName,
            TimeZone = cache.TimeZone,
            Latitude = cache.Latitude,
            Longitude = cache.Longitude
        };
    }

    private async Task<IpGeoDto?> FetchFromProviderAndUpdateCacheAsync(string ip)
    {
        try
        {
            var dto = await _geoProvider.FetchIpInfoAsync(ip);
            if (dto == null)
                return null;

            await _cacheRepo.AddOrUpdateAsync(new IpGeoCache
            {
                Ip = dto.Ip,
                CountryCode = dto.CountryCode,
                CountryName = dto.CountryName,
                TimeZone = dto.TimeZone,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                LastFetchedUtc = DateTime.UtcNow
            });

            await _cacheRepo.SaveChangesAsync();

            return dto;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error fetching geo for IP {Ip}", ip);
            throw; // global error handler will turn this into 500
        }
    }

    private static Result<string[]> ValidateAndNormalizeIps(string[]? ips)
    {
        if (ips == null || ips.Length == 0)
            return Result<string[]>.Failure("At least one IP is required.", ErrorType.Validation);

        var normalized = ips
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
            return Result<string[]>.Failure("At least one valid IP is required.", ErrorType.Validation);

        foreach (var ip in normalized)
        {
            if (!IPAddress.TryParse(ip, out _))
                return Result<string[]>.Failure($"Invalid IP format: '{ip}'.", ErrorType.Validation);
        }

        return Result<string[]>.Success(normalized);
    }

    private async Task<Batch> CreateAndPersistBatchAsync(string[] ips)
    {
        var batch = new Batch
        {
            Status = BatchStatus.Pending,
            TotalCount = ips.Length,
            ProcessedCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        batch.Items = ips
            .Select(ip => new BatchItem
            {
                Batch = batch,
                Ip = ip,
                Status = BatchItemStatus.Pending,
                Attempts = 0
            })
            .ToList();

        await _batchRepo.CreateAsync(batch);
        
        return batch;
    }

    private static Result<Guid> ValidateBatchId(Guid batchId)
    {
        if (batchId == Guid.Empty)
            return Result<Guid>.Failure("Batch id is required.", ErrorType.Validation);

        return Result<Guid>.Success(batchId);
    }

    private static BatchStatusDto MapToBatchStatusDto(Batch batch)
    {
        var eta = ComputeEtaUtc(batch);

        return new BatchStatusDto
        {
            BatchId = batch.Id,
            Processed = batch.ProcessedCount,
            Total = batch.TotalCount,
            Status = batch.Status.ToString(),
            StartedAtUtc = batch.StartedAtUtc,
            CompletedAtUtc = batch.CompletedAtUtc,
            EstimatedCompletionUtc = eta
        };
    }

    private static DateTime? ComputeEtaUtc(Batch batch)
    {
        if (batch.TotalCount == 0 ||
            batch.ProcessedCount == 0 ||
            batch.AverageMsPerItem == null ||
            batch.StartedAtUtc == null)
        {
            return null;
        }

        if (batch.Status == BatchStatus.Completed)
            return batch.CompletedAtUtc;

        var remaining = batch.TotalCount - batch.ProcessedCount;
        if (remaining <= 0)
            return batch.CompletedAtUtc ?? DateTime.UtcNow;

        var remainingMs = remaining * batch.AverageMsPerItem.Value;

        return DateTime.UtcNow.AddMilliseconds(remainingMs);
    }
}
