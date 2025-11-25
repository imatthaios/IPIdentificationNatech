using System.Collections.Concurrent;
using Application.Interfaces.Repositories;
using Domain.Entities;

namespace UnitTests.Fakes;

public class InMemoryGeoCacheRepository : IGeoCacheRepository
{
    private readonly ConcurrentDictionary<string, IpGeoCache> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IpGeoCache?> GetAsync(string ip, CancellationToken token = default)
    {
        _cache.TryGetValue(ip, out var value);
        return Task.FromResult(value);
    }

    public Task<IpGeoCache?> GetValidAsync(string ip, TimeSpan ttl, CancellationToken token = default)
    { 
          _cache.TryGetValue(ip, out var value);

        if (value == null)
            return Task.FromResult<IpGeoCache?>(null);

        if (DateTime.UtcNow - value.LastFetchedUtc > ttl)
            return Task.FromResult<IpGeoCache?>(null);

        return Task.FromResult<IpGeoCache?>(value);
    }

    public Task AddOrUpdateAsync(IpGeoCache entity, CancellationToken token = default)
    {
        _cache[entity.Ip] = entity;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, IpGeoCache> Snapshot() => _cache;
}