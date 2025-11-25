using System.Net;
using System.Text;
using Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Infrastructure.Services;
using Moq;

namespace UnitTests.Infrastructure;

public class GeoProviderClientMappingTests
{
    [Fact]
    public async Task FetchIpInfoAsync_Maps_IpGeoResponse_To_IpGeoDto()
    {
        // Arrange
        const string ip = "8.8.8.8";

        const string json = """
                            {
                              "data": {
                                "ip": "8.8.8.8",
                                "location": {
                                  "country": {
                                    "alpha2": "US",
                                    "name": "United States"
                                  },
                                  "latitude": 37.751,
                                  "longitude": -97.822
                                },
                                "timezone": {
                                  "id": "America/Chicago"
                                }
                              }
                            }
                            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var handler = new StubHttpMessageHandler(_ => response);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://freegeoip.app/")
        };

        var options = Options.Create(new IpGeoProviderOptions
        {
            BaseUrl = "https://freegeoip.app/"
        });

        var logger = Mock.Of<ILogger<GeoProviderClient>>();

        var sut = new GeoProviderClient(httpClient, options, logger);

        // Act
        var dto = await sut.FetchIpInfoAsync(ip, CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.Ip.Should().Be("8.8.8.8");
        dto.CountryCode.Should().Be("US");
        dto.CountryName.Should().Be("United States");
        dto.TimeZone.Should().Be("America/Chicago");
        dto.Latitude.Should().Be(37.751);
        dto.Longitude.Should().Be(-97.822);
    }

    [Fact]
    public async Task FetchIpInfoAsync_When_HttpRequestException_Returns_Null()
    {
        // Arrange
        const string ip = "1.1.1.1";

        var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("boom"));

        var httpClient = new HttpClient(handler);

        var options = Options.Create(new IpGeoProviderOptions
        {
            BaseUrl = "https://freegeoip.app/"
        });

        var logger = Mock.Of<ILogger<GeoProviderClient>>();
        var sut = new GeoProviderClient(httpClient, options, logger);

        // Act
        var dto = await sut.FetchIpInfoAsync(ip, CancellationToken.None);

        // Assert
        dto.Should().BeNull();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
