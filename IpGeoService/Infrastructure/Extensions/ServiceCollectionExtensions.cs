using System.Net;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Infrastructure.Configuration;
using Infrastructure.Data;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured.");
            }

            services.AddDbContext<IpGeoDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
            });

            services.Configure<IpGeoProviderOptions>(configuration.GetSection("IpGeoProvider"));
            services.Configure<IpGeoCacheOptions>(configuration.GetSection("IpGeoCache"));

            services.AddHttpClient<IGeoProviderClient, GeoProviderClient>()
                .AddPolicyHandler(GetGeoRetryPolicy());
            
            services.AddHttpClient<IGeoProviderClient, GeoProviderClient>(
                    (sp, httpClient) =>
                    {
                        var opts = sp.GetRequiredService<IOptions<IpGeoProviderOptions>>().Value;

                        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                        {
                            throw new InvalidOperationException("IpGeoProviderOptions.BaseUrl is not configured.");
                        }

                        httpClient.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/'), UriKind.Absolute);
                        httpClient.Timeout = TimeSpan.FromSeconds(10);

                        if (string.IsNullOrWhiteSpace(opts.ApiKey)) return;
                        httpClient.DefaultRequestHeaders.Remove("apikey");
                        httpClient.DefaultRequestHeaders.Add("apikey", opts.ApiKey);
                    })
                .AddPolicyHandler(GetGeoRetryPolicy());
            
            services.AddHostedService<GeoCacheCleanupService>();

            services.AddScoped<IBatchRepository, BatchRepository>();
            services.AddScoped<IBatchItemRepository, BatchItemRepository>();
            services.AddScoped<IGeoCacheRepository, GeoCacheRepository>();

            services.AddSingleton<ChannelBackgroundBatchProcessor>();

            services.AddSingleton<IBackgroundBatchProcessor>(sp =>
                sp.GetRequiredService<ChannelBackgroundBatchProcessor>());

            services.AddHostedService(sp =>
                sp.GetRequiredService<ChannelBackgroundBatchProcessor>());

            services.AddScoped<DataInitializer>();

            return services;
        }
        
        private static IAsyncPolicy<HttpResponseMessage> GetGeoRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == (HttpStatusCode)429)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250))
                );
        }
    }
}
