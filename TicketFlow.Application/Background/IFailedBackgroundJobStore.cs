using TicketFlow.Domain.Background;

namespace TicketFlow.Application.Background;

public interface IFailedBackgroundJobStore
{
    Task SaveAsync(FailedBackgroundJob failedJob, CancellationToken cancellationToken = default);
}