using System.Net.Http.Json;
using Application.Dtos;
using Application.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.External.IpGeoResponse;
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
        ILogger<GeoProviderClient> logger)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("IpGeoProviderOptions.BaseUrl is not configured.");
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Remove("apikey");
            _http.DefaultRequestHeaders.Add("apikey", _options.ApiKey);
        }

        if (_http.Timeout == TimeSpan.Zero)
        {
            _http.Timeout = TimeSpan.FromSeconds(10);
        }
    }

    public async Task<IpGeoDto?> FetchIpInfoAsync(
        string ip,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        var url = $"{_options.BaseUrl.TrimEnd('/')}/info?apikey={_options.ApiKey}&ip={ip}";
        
        try
        {
            var httpResponse = await _http.GetAsync(url, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Geo provider returned non-success status code {StatusCode} for IP {Ip} using {Url}",
                    (int)httpResponse.StatusCode,
                    ip,
                    url);

                return null;
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<IpGeoResponse>(
                cancellationToken: cancellationToken);

            if (response?.Data is null) return null;

            var data = response.Data;

            return new IpGeoDto
            {
                Ip = data.Ip,
                CountryCode = data.Location.Country.Alpha2,
                CountryName = data.Location.Country.Name,
                TimeZone = data.Timezone?.Id,
                Latitude = data.Location.Latitude,
                Longitude = data.Location.Longitude
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HttpRequestException while fetching geo for IP {Ip} using {Url}", ip, url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching geo for IP {Ip} using {Url}", ip, url);
            throw;
        }
    }
}
