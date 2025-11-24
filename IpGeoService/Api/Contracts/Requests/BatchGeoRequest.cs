namespace Api.Contracts.Requests;

public class BatchGeoRequest
{
    public List<string> Ips { get; set; } = [];
}