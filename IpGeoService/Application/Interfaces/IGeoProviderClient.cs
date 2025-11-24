using Application.Dtos;

namespace Application.Interfaces;

public interface IGeoProviderClient
{
    Task<IpGeoDto?> FetchIpInfoAsync(string ip, CancellationToken cancellationToken = default);
}