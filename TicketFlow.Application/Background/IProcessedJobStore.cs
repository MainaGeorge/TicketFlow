namespace TicketFlow.Application.Background;

public interface IProcessedJobStore
{
    Task<bool> ExistsAsync(string idempotencyKey,  CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string idempotencyKey,  CancellationToken cancellationToken = default);
}
