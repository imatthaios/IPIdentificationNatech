using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public interface IDataInitializer
{
    Task InitializeAsync();
}

public class DataInitializer : IDataInitializer
{
    private readonly IpGeoDbContext _db;
    private readonly ILogger<DataInitializer> _log;

    public DataInitializer(IpGeoDbContext db, ILogger<DataInitializer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task InitializeAsync()
    {
        // Ensure database exists & migrations applied
        await _db.Database.MigrateAsync();

        // Simple example seed to prove things work
        if (!await _db.IpGeoCache.AnyAsync())
        {
            _db.IpGeoCache.Add(new IpGeoCache
            {
                Ip = "8.8.8.8",
                CountryCode = "US",
                CountryName = "United States",
                TimeZone = "America/Los_Angeles",
                Latitude = 37.751,
                Longitude = -97.822,
                LastFetchedUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            _log.LogInformation("Seeded sample IP cache entry for 8.8.8.8");
        }
    }
}