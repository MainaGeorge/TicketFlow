using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TicketFlow.Infrastructure.Background;

namespace TicketFlow.Tests.Infrastructure.Background;

public class QueuedBackgroundServiceTests
{
    [Fact]
    public async Task Worker_ExecutesQueuedWorkItem()
    {
        var queue = new BackgroundTaskQueue();

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(
            (_, _) =>
            {
                completion.SetResult(true);
                return ValueTask.CompletedTask;
            });

        await worker.StartAsync(CancellationToken.None);

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await worker.StopAsync(CancellationToken.None);

        Assert.True(completion.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Worker_CreatesSeparateScopeForEachWorkItem()
    {
        var queue = new BackgroundTaskQueue();

        var services = new ServiceCollection();

        services.AddScoped<ScopedMarker>();

        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance);

        var ids = new List<Guid>();

        var firstCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var secondCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(
            (provider, _) =>
            {
                ids.Add(provider.GetRequiredService<ScopedMarker>().Id);

                firstCompleted.SetResult(true);

                return ValueTask.CompletedTask;
            });

        await queue.QueueAsync(
            (provider, _) =>
            {
                ids.Add(provider.GetRequiredService<ScopedMarker>().Id);

                secondCompleted.SetResult(true);

                return ValueTask.CompletedTask;
            });

        await worker.StartAsync(CancellationToken.None);

        await firstCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        await secondCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, ids.Count);
        Assert.NotEqual(ids[0], ids[1]);
    }

    [Fact]
    public async Task Worker_WhenWorkItemThrows_ContinuesProcessingNextItem()
    {
        var queue = new BackgroundTaskQueue();

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance);

        var secondCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync((_, _) => throw new InvalidOperationException("Simulated background failure"));

        await queue.QueueAsync(
            (_, _) =>
            {
                secondCompleted.SetResult(true);
                return ValueTask.CompletedTask;
            });

        await worker.StartAsync(CancellationToken.None);

        await secondCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await worker.StopAsync(CancellationToken.None);

        Assert.True(secondCompleted.Task.IsCompletedSuccessfully);
    }

    private sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
