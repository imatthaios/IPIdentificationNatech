using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IBatchRepository
{
    Task<Batch?> GetByIdAsync(Guid id);
    Task<Batch> CreateAsync(Batch batch);
    Task SaveChangesAsync();
}