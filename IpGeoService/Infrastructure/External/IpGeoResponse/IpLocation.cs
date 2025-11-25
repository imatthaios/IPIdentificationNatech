namespace Infrastructure.External.IpGeoResponse;

public class IpLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public IpCountry Country { get; set; } = null!;
}