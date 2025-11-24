using System.Net.Http.Json;
using Application.Dtos;
using Application.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class GeoProviderClient : IGeoProviderClient
{
    private readonly HttpClient _http;
    private readonly IpGeoProviderOptions _options;
    private readonly ILogger<GeoProviderClient> _logger;

    public GeoProviderClient(
        HttpClient httpClient,
        IOptions<IpGeoProviderOptions> options,
        ILogger<GeoProviderClient> log)
    {
        _options = options.Value;
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = log ?? throw new ArgumentNullException(nameof(log));
        
        _http.BaseAddress = new Uri(_options.BaseUrl);
        
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _http.BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return;
        
        _http.DefaultRequestHeaders.Remove("apikey");
        _http.DefaultRequestHeaders.Add("apikey", _options.ApiKey);
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<IpGeoDto?> FetchIpInfoAsync(string ip, CancellationToken cancellationToken = default)
    {
        var url = $"json/{ip}";

        try
        {
            var response = await _http.GetFromJsonAsync<IpGeoResponse>(
                url,
                cancellationToken);

            if (response is null)
                return null;

            return new IpGeoDto
            {
                Ip = response.Ip,
                CountryCode = response.CountryCode,
                CountryName = response.CountryName,
                TimeZone = response.TimeZone,
                Latitude = response.Latitude,
                Longitude = response.Longitude
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HttpRequestException while fetching geo for IP {Ip}", ip);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching geo for IP {Ip}", ip);
            throw;
        }
    }
}
