using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntegrationTests
{
    public class GeoEndpointsIntegrationTests
        : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;

        public GeoEndpointsIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetGeo_Should_Return_200_And_Valid_Body()
        {
            // Arrange
            var ip = "8.8.8.8";

            // Act
            var response = await _client.GetAsync($"/api/geo/{ip}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var dto = await response.Content.ReadFromJsonAsync<IpGeoDto>();
            dto.Should().NotBeNull();
            dto!.Ip.Should().Be(ip);
            dto.CountryCode.Should().Be("GR");        // from FakeGeoProviderClient
            dto.CountryName.Should().Be("Greece");
            dto.TimeZone.Should().Be("Europe/Athens");
        }

        [Fact]
        public async Task Batch_Endpoints_Should_Process_Batch_Until_Completed()
        {
            // Arrange
            var ips = new[] { "1.1.1.1", "8.8.8.8" };

            // Act – enqueue batch
            var postResponse = await _client.PostAsJsonAsync("/api/geo/batch", new
            {
                ips
            });

            postResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var accepted = await postResponse.Content.ReadFromJsonAsync<BatchAcceptedDto>();
            accepted.Should().NotBeNull();

            var batchId = accepted!.BatchId;

            // Poll GET /api/geo/batch/{id} until completed or timeout
            BatchStatusDto? status = null;
            var stopAt = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < stopAt)
            {
                var statusResponse = await _client.GetAsync($"/api/geo/batch/{batchId}");
                statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

                status = await statusResponse.Content.ReadFromJsonAsync<BatchStatusDto>();
                status.Should().NotBeNull();

                if (status!.Status == "Completed")
                    break;

                await Task.Delay(200);
            }

            // Assert
            status.Should().NotBeNull();
            status!.Status.Should().Be("Completed");
            status.Processed.Should().Be(status.Total);
            status.Total.Should().Be(ips.Length);
        }
    }

    /// <summary>
    /// Custom test host that:
    /// - Replaces SQL Server with InMemory EF
    /// - Replaces real IGeoProviderClient with a deterministic fake
    /// - Keeps the real background processor & pipeline
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace IpGeoDbContext with InMemory
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<IpGeoDbContext>));

                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<IpGeoDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IpGeoIntegrationTestsDb");
                });

                // Replace IGeoProviderClient with fake
                var geoClientDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IGeoProviderClient));

                if (geoClientDescriptor != null)
                    services.Remove(geoClientDescriptor);

                services.AddSingleton<IGeoProviderClient, FakeGeoProviderClient>();

                // Optional: reduce logging noise in tests
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });
        }
    }

    /// <summary>
    /// Deterministic fake implementation of IGeoProviderClient used in integration tests.
    /// Returns a stable, fast response with Greek geo info for any IP.
    /// </summary>
    public class FakeGeoProviderClient : IGeoProviderClient
    {
        public Task<IpGeoDto?> FetchIpInfoAsync(string ip, CancellationToken cancellationToken = default)
        {
            var dto = new IpGeoDto
            {
                Ip = ip,
                CountryCode = "GR",
                CountryName = "Greece",
                TimeZone = "Europe/Athens",
                Latitude = 37.97945,
                Longitude = 23.71622
            };

            return Task.FromResult<IpGeoDto?>(dto);
        }
    }
}
