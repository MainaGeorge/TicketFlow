namespace TicketFlow.Application.Background;

public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(BackgroundWorkItem workItem, CancellationToken cancellationToken = default);
    ValueTask<BackgroundWorkItem> DequeueAsync(CancellationToken cancellationToken);
}