using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class BatchItemRepository : IBatchItemRepository
{
    private readonly IpGeoDbContext _db;

    public BatchItemRepository(IpGeoDbContext db)
    {
        _db = db;
    }

    public Task AddRangeAsync(IEnumerable<BatchItem> items)
    {
        _db.BatchItems.AddRange(items);
        return Task.CompletedTask;
    }

    public async Task<List<BatchItem>> GetByBatchIdAsync(Guid batchId)
    {
        return await _db.BatchItems
            .Where(i => i.BatchId == batchId)
            .ToListAsync();
    }

    public async Task<BatchItem?> GetPendingItemAsync(Guid batchId, string ip)
    {
        return await _db.BatchItems
            .Where(i => i.BatchId == batchId &&
                        i.Ip == ip &&
                        i.Status == BatchItemStatus.Pending)
            .FirstOrDefaultAsync();
    }

    public async Task<List<BatchItem>> GetPendingItemsAsync(int take, int maxAttempts)
    {
        return await _db.BatchItems
            .Where(i => i.Status == BatchItemStatus.Pending && i.Attempts < maxAttempts)
            .OrderBy(i => i.StartedAtUtc ?? DateTime.MaxValue)
            .Take(take)
            .ToListAsync();
    }

    public Task UpdateAsync(BatchItem item)
    {
        _db.BatchItems.Update(item);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}