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

    public async Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Batches
            .Include(b => b.Items)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Batch> CreateAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        await _db.Batches.AddAsync(batch, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) 
        => _db.SaveChangesAsync(cancellationToken);
}