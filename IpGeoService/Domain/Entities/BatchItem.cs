using Domain.Enums;

namespace Domain.Entities;

public class BatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public required Batch Batch { get; set; }

    public string Ip { get; set; } = string.Empty;
    public BatchItemStatus Status { get; set; } = BatchItemStatus.Pending;

    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? TimeZone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; } = 0;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? DurationMs { get; set; }
}