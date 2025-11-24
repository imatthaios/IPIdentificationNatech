using Application.Interfaces.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class BatchRepository : IBatchRepository
{
    private readonly IpGeoDbContext _db;

    public BatchRepository(IpGeoDbContext db)
    {
        _db = db;
    }

    public async Task<Batch?> GetByIdAsync(Guid id)
    {
        return await _db.Batches
            .Include(b => b.Items)
            .SingleOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Batch> CreateAsync(Batch batch)
    {
        _db.Batches.Add(batch);
        await _db.SaveChangesAsync();
        return batch;
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}