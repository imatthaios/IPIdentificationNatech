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

    public Task<IpGeoCache?> GetAsync(string ip)
    {
        return _db.IpGeoCache.SingleOrDefaultAsync(x => x.Ip == ip);
    }

    public async Task AddOrUpdateAsync(IpGeoCache cacheEntry)
    {
        var existing = await _db.IpGeoCache.FindAsync(cacheEntry.Ip);
        if (existing != null)
        {
            _db.Entry(existing).CurrentValues.SetValues(cacheEntry);
        }
        else
        {
            await _db.IpGeoCache.AddAsync(cacheEntry);
        }
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}