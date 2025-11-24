using Application.Common;
using Application.Dtos;

namespace Application.Interfaces;

public interface IGeoApplicationService
{
    Task<Result<IpGeoDto>> GetGeoForIpAsync(string ip);
    Task<Result<BatchAcceptedDto>> EnqueueBatchAsync(string[] ips);
    Task<Result<BatchStatusDto>> GetBatchStatusAsync(Guid batchId);
}