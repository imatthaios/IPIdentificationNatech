using System.Collections.Concurrent;
using Application.Interfaces.Repositories;
using Domain.Entities;

namespace UnitTests.Fakes
{
    /// <summary>
    /// Simple thread-safe in-memory implementation of IGeoCacheRepository
    /// for unit testing the background processor.
    /// </summary>
    public class InMemoryGeoCacheRepository : IGeoCacheRepository
    {
        private readonly ConcurrentDictionary<string, IpGeoCache> _cache = new(StringComparer.OrdinalIgnoreCase);

        public Task<IpGeoCache?> GetAsync(string ip)
        {
            _cache.TryGetValue(ip, out var value);
            return Task.FromResult(value);
        }

        public Task AddOrUpdateAsync(IpGeoCache entity)
        {
            _cache[entity.Ip] = entity;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync()
        {
            return Task.CompletedTask;
        }

        public IReadOnlyDictionary<string, IpGeoCache> Snapshot() => _cache;
    }
}