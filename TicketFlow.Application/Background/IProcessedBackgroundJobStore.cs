namespace TicketFlow.Application.Background;

public interface IProcessedBackgroundJobStore
{
    Task<bool> ExistsAsync(string idempotencyKey,  CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string idempotencyKey,  CancellationToken cancellationToken = default);
}
