using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IBatchRepository
{
    Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Batch> CreateAsync(Batch batch, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}