namespace Application.Dtos;

public class BatchAcceptedDto
{
    public Guid BatchId { get; set; }
    public string StatusUrl { get; set; } = string.Empty;
}