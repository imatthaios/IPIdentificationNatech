using Domain.Enums;

namespace Domain.Entities;

public class Batch
{
    public Guid Id { get; set; }

    public BatchStatus Status { get; set; }

    public int TotalCount { get; set; }

    public int ProcessedCount { get; set; }

    /// <summary>
    /// Average processing time per item in milliseconds.
    /// </summary>
    public double? AverageMsPerItem { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<BatchItem> Items { get; set; } = new List<BatchItem>();

    public Batch()
    {
        // EF and manual initialization
    }

    public Batch(Guid id, int totalCount)
    {
        Id = id;
        TotalCount = totalCount;
        CreatedAtUtc = DateTime.UtcNow;
        Status = BatchStatus.Pending;
        Items = new List<BatchItem>();
    }

    /// <summary>
    /// Adds a new item to this batch and wires up navigation properties.
    /// </summary>
    public void AddItem(BatchItem item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));

        item.Batch = this;
        item.BatchId = Id;

        Items.Add(item);
    }

    /// <summary>
    /// Returns the first pending item for a given IP.
    /// </summary>
    public BatchItem? GetPendingItem(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;

        return Items.FirstOrDefault(i =>
            i.Ip.Equals(ip, StringComparison.OrdinalIgnoreCase) &&
            i.Status == BatchItemStatus.Pending);
    }

    /// <summary>
    /// Increments the processed count (to be called after an item is finalized).
    /// </summary>
    public void IncrementProcessed()
    {
        ProcessedCount++;
    }

    /// <summary>
    /// Recalculates the rolling average processing time based on a newly processed item.
    /// </summary>
    public void UpdateStats(BatchItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.DurationMs.HasValue) return;

        var duration = (double)item.DurationMs.Value;

        if (ProcessedCount <= 0 || AverageMsPerItem is null)
        {
            AverageMsPerItem = duration;
            return;
        }

        // Rolling average:
        // newAvg = (prevAvg * (n-1) + newDuration) / n
        var previous = AverageMsPerItem.Value;
        AverageMsPerItem =
            ((previous * (ProcessedCount - 1)) + duration) / ProcessedCount;
    }

    /// <summary>
    /// Updates batch status and timestamps based on current progress.
    /// </summary>
    public void UpdateStatusForProgress()
    {
        if (TotalCount == 0 || ProcessedCount >= TotalCount)
        {
            Status = BatchStatus.Completed;
            CompletedAtUtc ??= DateTime.UtcNow;
            return;
        }

        if (Status != BatchStatus.Pending) return;
        
        Status = BatchStatus.Running;
        StartedAtUtc ??= DateTime.UtcNow;
    }
}
