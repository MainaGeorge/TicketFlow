using System.Threading.Channels;
using TicketFlow.Application.Background;

namespace TicketFlow.Infrastructure.Background;

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<BackgroundWorkItem> _queue;

    public BackgroundTaskQueue()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _queue = Channel.CreateBounded<BackgroundWorkItem> (options);
    }

    public async ValueTask<BackgroundWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    public async ValueTask QueueAsync(BackgroundWorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        await _queue.Writer.WriteAsync(workItem, cancellationToken);
    }
}