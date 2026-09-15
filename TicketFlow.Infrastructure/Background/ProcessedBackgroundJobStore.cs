using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Background;
using TicketFlow.Domain.Background;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Background;

public class ProcessedBackgroundJobStore(AppDbContext context) : IProcessedBackgroundJobStore
{
    public async Task<bool> ExistsAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await context
            .ProcessedBackgroundJobs
            .AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task MarkProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var processedJob = new ProcessedBackgroundJob
        {
            IdempotencyKey = idempotencyKey,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        await context.ProcessedBackgroundJobs.AddAsync(processedJob, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }
}
