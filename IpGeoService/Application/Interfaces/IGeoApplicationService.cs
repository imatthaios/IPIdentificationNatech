using Application.Common;
using Application.Dtos;

namespace Application.Interfaces;

public interface IGeoApplicationService
{
    Task<Result<IpGeoDto>> GetGeoForIpAsync(string ip, CancellationToken cancellationToken = default);

    Task<Result<BatchAcceptedDto>> EnqueueBatchAsync(string[] ips, CancellationToken cancellationToken = default);

    Task<Result<BatchStatusDto>> GetBatchStatusAsync(Guid batchId, CancellationToken cancellationToken = default);
}