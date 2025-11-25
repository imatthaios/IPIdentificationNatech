using Application.Interfaces.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class GeoCacheRepository : IGeoCacheRepository
{
    private readonly IpGeoDbContext _db;

    public GeoCacheRepository(IpGeoDbContext db)
    {
        _db = db;
    }

    public Task<IpGeoCache?> GetAsync(
        string ip,
        CancellationToken cancellationToken = default)
    {
        return _db.IpGeoCache
            .SingleOrDefaultAsync(x => x.Ip == ip, cancellationToken);
    }

    public async Task AddOrUpdateAsync(
        IpGeoCache cacheEntry,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.IpGeoCache
            .FindAsync([cacheEntry.Ip], cancellationToken);

        if (existing != null)
        {
            _db.Entry(existing).CurrentValues.SetValues(cacheEntry);
        }
        else
        {
            await _db.IpGeoCache.AddAsync(cacheEntry, cancellationToken);
        }
    }

    public async Task<IpGeoCache?> GetValidAsync(
        string ip,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var entry = await _db.IpGeoCache
            .FindAsync([ip], cancellationToken);

        if (entry == null)
            return null;

        var age = DateTime.UtcNow - entry.LastFetchedUtc;

        return age <= ttl ? entry : null;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}