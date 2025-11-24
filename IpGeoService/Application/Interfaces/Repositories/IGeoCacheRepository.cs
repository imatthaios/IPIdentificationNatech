using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IGeoCacheRepository
{
    Task<IpGeoCache?> GetAsync(string ip);
    Task AddOrUpdateAsync(IpGeoCache cacheEntry);
    Task SaveChangesAsync();
}