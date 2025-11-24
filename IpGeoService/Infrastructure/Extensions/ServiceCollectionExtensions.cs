using Application.Interfaces;
using Application.Interfaces.Repositories;
using Infrastructure.Configuration;
using Infrastructure.Data;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;

namespace Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // DB
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<IpGeoDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IBatchItemRepository, BatchItemRepository>();
        services.AddScoped<IGeoCacheRepository, GeoCacheRepository>();

        // Data initializer
        services.AddScoped<IDataInitializer, DataInitializer>();

        // Background processor
        services.AddHostedService<ChannelBackgroundBatchProcessor>();
        services.AddSingleton<IBackgroundBatchProcessor>(sp =>
            (ChannelBackgroundBatchProcessor)sp.GetRequiredService<IHostedService>());
        
        // options
        services.Configure<IpGeoProviderOptions>(
            configuration.GetSection(IpGeoProviderOptions.SectionName));

        // HttpClient
        services
            .AddHttpClient<IGeoProviderClient, GeoProviderClient>()
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
