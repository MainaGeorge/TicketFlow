using TicketFlow.Application.Background;
using TicketFlow.Domain.Background;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Background;

public class FailedBackgroundJobStore(AppDbContext context) : IFailedBackgroundJobStore
{
    public async Task SaveAsync(FailedBackgroundJob failedJob, CancellationToken cancellationToken = default)
    {
        await context.FailedBackgroundJobs.AddAsync(failedJob, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }
}