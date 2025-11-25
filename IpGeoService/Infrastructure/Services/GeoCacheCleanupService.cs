using Infrastructure.Configuration;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class GeoCacheCleanupService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<GeoCacheCleanupService> _log;
    private readonly TimeSpan _ttl;
    private readonly TimeSpan _interval;

    public GeoCacheCleanupService(
        IServiceProvider provider,
        IOptions<IpGeoCacheOptions> options,
        ILogger<GeoCacheCleanupService> log)
    {
        _provider = provider;
        _log = log;

        var cfg = options.Value;

        _ttl = TimeSpan.FromHours(cfg.TtlHours);
        _interval = TimeSpan.FromHours(cfg.CleanupIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "GeoCacheCleanupService started. TTL={TTL} interval={Interval}.", 
            _ttl, _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IpGeoDbContext>();

                var cutoff = DateTime.UtcNow - _ttl;

                var staleEntries = db.IpGeoCache
                    .Where(x => x.LastFetchedUtc < cutoff)
                    .ToList();

                if (staleEntries.Any())
                {
                    _log.LogInformation(
                        "Purging {Count} stale entries older than {Cutoff}",
                        staleEntries.Count, cutoff);

                    db.IpGeoCache.RemoveRange(staleEntries);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error during IpGeoCache cleanup");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
