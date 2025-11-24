namespace Application.Dtos;

public class IpGeoDto
{
    public string Ip { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? TimeZone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}