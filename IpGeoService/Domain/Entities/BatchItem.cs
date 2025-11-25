using Domain.Enums;

namespace Domain.Entities;

public class BatchItem
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public Batch Batch { get; set; } = null!;

    public string Ip { get; set; } = null!;

    public BatchItemStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }

    public string? ErrorMessage { get; set; }

    public string? CountryCode { get; set; }

    public string? CountryName { get; set; }

    public string? TimeZone { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public BatchItem()
    {
    }

    public BatchItem(Guid id, Guid batchId, string ip)
    {
        Id = id;
        BatchId = batchId;
        Ip = ip;
        Status = BatchItemStatus.Pending;
        Attempts = 0;
    }

    /// <summary>
    /// Marks the item as running, increments attempts and sets start time.
    /// </summary>
    public void MarkRunning()
    {
        Status = BatchItemStatus.Running;
        Attempts++;
        StartedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the item as failed and stores an error message.
    /// </summary>
    public void MarkFailed(string message)
    {
        Status = BatchItemStatus.Failed;
        ErrorMessage = message;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the item as succeeded and sets geo data fields.
    /// </summary>
    public void MarkSucceeded(
        string? countryCode,
        string? countryName,
        string? timeZone,
        double? latitude,
        double? longitude)
    {
        Status = BatchItemStatus.Succeeded;
        ErrorMessage = null;

        CountryCode = countryCode;
        CountryName = countryName;
        TimeZone = timeZone;
        Latitude = latitude;
        Longitude = longitude;

        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the measured processing duration in milliseconds.
    /// </summary>
    public void SetDuration(TimeSpan elapsed)
    {
        DurationMs = (int)elapsed.TotalMilliseconds;
    }
}
