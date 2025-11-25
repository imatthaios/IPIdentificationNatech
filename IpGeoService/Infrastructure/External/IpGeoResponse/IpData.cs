namespace Infrastructure.External.IpGeoResponse;

public class IpData
{
    public string Ip { get; set; } = null!;
    public IpLocation Location { get; set; } = null!;
    public IpTimezone? Timezone { get; set; }
}