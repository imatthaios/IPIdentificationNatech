using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IGeoCacheRepository
{
    Task<IpGeoCache?> GetAsync(string ip, CancellationToken token = default);
    Task AddOrUpdateAsync(IpGeoCache cacheEntry, CancellationToken token = default);
    Task<IpGeoCache?> GetValidAsync(string ip, TimeSpan ttl, CancellationToken token = default);
    Task SaveChangesAsync(CancellationToken token = default);
}