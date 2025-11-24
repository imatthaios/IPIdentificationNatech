namespace Application.Dtos;

public class BatchStatusDto
{
    public Guid BatchId { get; set; }
    public int Processed { get; set; }
    public int Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? EstimatedCompletionUtc { get; set; }
}