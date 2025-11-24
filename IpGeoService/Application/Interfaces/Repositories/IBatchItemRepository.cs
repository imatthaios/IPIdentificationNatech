using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IBatchItemRepository
{
    Task AddRangeAsync(IEnumerable<BatchItem> items);
    Task<List<BatchItem>> GetByBatchIdAsync(Guid batchId);
    Task<BatchItem?> GetPendingItemAsync(Guid batchId, string ip);
    Task<List<BatchItem>> GetPendingItemsAsync(int take, int maxAttempts);
    Task UpdateAsync(BatchItem item);
    Task SaveChangesAsync();
}