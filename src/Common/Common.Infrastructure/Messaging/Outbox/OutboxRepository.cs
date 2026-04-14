using Common.Application.Interfaces;
using Common.Domain.Messaging.Outbox;
using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Common.Infrastructure.Messaging.Outbox;

/// <summary>
/// EF Core implementation of IOutboxRepository.
/// Registers via AddScoped — the same DbContext lifetime as the rest of the app.
/// </summary>
public class OutboxRepository : IOutboxRepository
{
    private readonly BaseDbContext _db;

    public OutboxRepository(BaseDbContext db) => _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await _db.Set<OutboxMessage>().AddAsync(message, ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize, CancellationToken ct = default)
    {
        return await _db.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(OutboxMessage message, CancellationToken ct = default)
    {
        message.MarkAsProcessed();
        _db.Set<OutboxMessage>().Update(message);
        await Task.CompletedTask; // keep async signature even though no extra async work
    }

    public async Task MarkAsFailedAsync(OutboxMessage message, CancellationToken ct = default)
    {
        message.MarkAsFailed();
        _db.Set<OutboxMessage>().Update(message);
        await Task.CompletedTask;
    }

    public async Task DeleteProcessedOlderThanAsync(DateTime threshold, CancellationToken ct = default)
    {
        await _db.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAt < threshold)
            .ExecuteDeleteAsync(ct);
    }
}