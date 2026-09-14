using TicketFlow.Infrastructure.Background;

namespace TicketFlow.Tests.Infrastructure.Background;

public class BackgroundTaskQueueTest
{
    [Fact]
    public async Task QueueAsync_ThenDequeueAsync_ReturnsSameWorkItem()
    {
        var queue = new BackgroundTaskQueue();

        Func<IServiceProvider, CancellationToken, ValueTask> workItem = (_, _) => ValueTask.CompletedTask;

        await queue.QueueAsync(workItem);

        var result = await queue.DequeueAsync(CancellationToken.None);

        Assert.Same(workItem, result);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsItemsInFifoOrder()
    {
        var queue = new BackgroundTaskQueue();

        Func<IServiceProvider, CancellationToken, ValueTask> first = (_, _) => ValueTask.CompletedTask;
        Func<IServiceProvider, CancellationToken, ValueTask> second = (_, _) => ValueTask.CompletedTask;

        await queue.QueueAsync(first);
        await queue.QueueAsync(second);

        var firstResult = await queue.DequeueAsync(CancellationToken.None);
        var secondResult =await queue.DequeueAsync(CancellationToken.None);

        Assert.Same(first, firstResult);
        Assert.Same(second, secondResult);
    }

    [Fact]
    public async Task DequeueAsync_WhenQueueIsEmpty_WaitsForWork()
    {
        var queue = new BackgroundTaskQueue();

        var dequeueTask = queue.DequeueAsync(CancellationToken.None).AsTask();

        Assert.False(dequeueTask.IsCompleted);

        Func<IServiceProvider, CancellationToken, ValueTask> workItem = (_, _) => ValueTask.CompletedTask;

        await queue.QueueAsync(workItem);

        var result = await dequeueTask;

        Assert.Same(workItem, result);
    }

    [Fact]
    public async Task DequeueAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var queue = new BackgroundTaskQueue();

        using var cancellationTokenSource = new CancellationTokenSource();

        var dequeueTask = queue.DequeueAsync(cancellationTokenSource.Token).AsTask();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dequeueTask);
    }
}
