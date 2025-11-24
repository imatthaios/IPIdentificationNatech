namespace Application.Interfaces;

/// <summary>
/// Lightweight abstraction for queuing batch work for background processing.
/// Implementations should be registered in Infrastructure and handle persistence/processing.
/// The Application layer depends only on this contract.
/// </summary>
public interface IBackgroundBatchProcessor
{
    /// <summary>
    /// Queue a batch of IPs for processing.
    /// The implementation decides how to schedule/process these (in-memory channel, external queue, etc).
    /// This method should return quickly and not block on long-running processing.
    /// </summary>
    /// <param name="ips">Normalized, validated list of IP addresses.</param>
    /// <param name="batchId">The batch id that was persisted by the application layer.</param>
    Task QueueBatchAsync(IEnumerable<string> ips, Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional: peek at current queued work size (may be O(1) or approximate).
    /// Useful for health checks / throttling decisions.
    /// </summary>
    int? GetApproximateQueueLength();
}