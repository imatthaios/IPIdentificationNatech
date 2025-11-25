using System.Collections.Concurrent;
using Application.Interfaces.Repositories;
using Domain.Entities;

namespace UnitTests.Fakes
{
    /// <summary>
    /// Simple in-memory implementation of IBatchRepository
    /// for unit testing the background processor.
    /// </summary>
    public class InMemoryBatchRepository : IBatchRepository
    {
        private readonly ConcurrentDictionary<Guid, Batch> _batches =
            new ConcurrentDictionary<Guid, Batch>();

        public InMemoryBatchRepository(params Batch[] initialBatches)
        {
            foreach (var batch in initialBatches)
            {
                _batches[batch.Id] = batch;
            }
        }

        public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _batches.TryGetValue(id, out var batch);
            return Task.FromResult(batch);
        }

        public Task<Batch> CreateAsync(Batch batch, CancellationToken cancellationToken = default)
        {
            _batches[batch.Id] = batch;
            return Task.FromResult(batch);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // In-memory – nothing to persist
            return Task.CompletedTask;
        }
    }
}