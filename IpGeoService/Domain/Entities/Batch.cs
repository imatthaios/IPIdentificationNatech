using Domain.Enums;

namespace Domain.Entities;

public class Batch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public BatchStatus Status { get; set; } = BatchStatus.Pending;
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public double? AverageMsPerItem { get; set; }

    public ICollection<BatchItem> Items { get; set; } = new List<BatchItem>();
}
