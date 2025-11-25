namespace Infrastructure.Configuration;

public class IpGeoCacheOptions
{
    /// <summary>
    /// Time-to-live (in hours) for cached entries.
    /// </summary>
    public int TtlHours { get; set; } = 12;

    /// <summary>
    /// Interval (in hours) between cleanup runs.
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 6;
}