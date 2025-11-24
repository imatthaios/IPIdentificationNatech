namespace Infrastructure.Configuration;

public class IpGeoProviderOptions
{
    public const string SectionName = "IpGeoProvider";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}