using System;
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

            services.Configure<IpGeoProviderOptions>(
                configuration.GetSection("IpGeoProvider"));

            services.AddHttpClient<IGeoProviderClient, GeoProviderClient>(
                (sp, httpClient) =>
                {
                    var opts = sp.GetRequiredService<IOptions<IpGeoProviderOptions>>().Value;

                    if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                    {
                        throw new InvalidOperationException(
                            "IpGeoProviderOptions.BaseUrl is not configured.");
                    }

                    httpClient.BaseAddress = new Uri(opts.BaseUrl);
                });

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
    }
}
