using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TicketFlow.Application.Background;
using TicketFlow.Domain.Background;
using TicketFlow.Infrastructure.Background;

namespace TicketFlow.Tests.Infrastructure.Background;

public class QueuedBackgroundServiceTests
{
    [Fact]
    public async Task Worker_ExecutesQueuedWorkItem()
    {
        var queue = new BackgroundTaskQueue();

        var retryOptions = Options.Create(
           new BackgroundRetryOptions
           {
               MaxAttempts = 3,
               BaseDelay = TimeSpan.FromMilliseconds(10)
           });

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance,
            retryOptions);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(
            new BackgroundWorkItem(
                "TestJobType",
                "TestPayload",
                (_, _) =>
                {
                    completion.SetResult(true);
                    return ValueTask.CompletedTask;
                }));

        await worker.StartAsync(CancellationToken.None);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(completion.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Worker_CreatesSeparateScopeForEachWorkItem()
    {
        var queue = new BackgroundTaskQueue();

        var retryOptions = Options.Create(
           new BackgroundRetryOptions
           {
               MaxAttempts = 3,
               BaseDelay = TimeSpan.FromMilliseconds(10),
               JitterFactor = 0,
               MaxDelay = TimeSpan.FromMilliseconds(20)
           });

        var services = new ServiceCollection();
        services.AddScoped<ScopedMarker>();
        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance,
            retryOptions);

        var ids = new List<Guid>();
        var firstCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(
            new BackgroundWorkItem(
                "TestJobType",
                "TestPayload",
                (provider, _) =>
                {
                    ids.Add(provider.GetRequiredService<ScopedMarker>().Id);
                    firstCompleted.SetResult(true);
                    return ValueTask.CompletedTask;
                }));

        await queue.QueueAsync(
             new BackgroundWorkItem(
                "TestJobType",
                "TestPayload",
            (provider, _) =>
            {
                ids.Add(provider.GetRequiredService<ScopedMarker>().Id);
                secondCompleted.SetResult(true);
                return ValueTask.CompletedTask;
            }));

        await worker.StartAsync(CancellationToken.None);
        await firstCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await secondCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, ids.Count);
        Assert.NotEqual(ids[0], ids[1]);
    }

    [Fact]
    public async Task Worker_WhenWorkItemThrows_ContinuesProcessingNextItem()
    {
        var queue = new BackgroundTaskQueue();

        var retryOptions = Options.Create(
           new BackgroundRetryOptions
           {
               MaxAttempts = 3,
               BaseDelay = TimeSpan.FromMilliseconds(10),
               JitterFactor = 0,
               MaxDelay = TimeSpan.FromMilliseconds(20)
           });

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance,
            retryOptions);

        var secondCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(new BackgroundWorkItem("TestJobType", "TestPayload", (_, _) => throw new InvalidOperationException("Simulated background failure")));

        await queue.QueueAsync(
              new BackgroundWorkItem(
                "TestJobType",
                "TestPayload",
                (_, _) =>
                {
                    secondCompleted.SetResult(true);
                    return ValueTask.CompletedTask;
                }));

        await worker.StartAsync(CancellationToken.None);
        await secondCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(secondCompleted.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DequeueAsync_WhenTriesAreBelowThresholdAndNoMoreFailureOccurs_JobCompletes()
    {
        var queue = new BackgroundTaskQueue();
        var failedJobStoreMock = new Mock<IFailedBackgroundJobStore>();

        var retryOptions = Options.Create(
            new BackgroundRetryOptions
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10),
                JitterFactor = 0,
                MaxDelay = TimeSpan.FromMilliseconds(20)
            });

        var services = new ServiceCollection();
        services.AddScoped(_ => failedJobStoreMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new QueuedBackgroundService(
                queue,
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<QueuedBackgroundService>.Instance,
                retryOptions);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = 0;

        await queue.QueueAsync(new BackgroundWorkItem("TestJobType", "TestPayload", (_, _) =>
        {
            attempts++;

            if (attempts < 3)
            {
                return ValueTask.FromException(new TransientBackgroundException("Temporary failure"));
            }

            completion.SetResult(true);

            return ValueTask.CompletedTask;
        }));

        await worker.StartAsync(CancellationToken.None);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, attempts);
        failedJobStoreMock.Verify(x => x.SaveAsync(It.IsAny<FailedBackgroundJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Worker_WhenAllAttemptsFail_StopsAfterConfiguredMaxAttempts()
    {
        var queue = new BackgroundTaskQueue();
        var failedJobStoreMock = new Mock<IFailedBackgroundJobStore>();
        var failedJobCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        failedJobStoreMock
            .Setup(x => x.SaveAsync(It.IsAny<FailedBackgroundJob>(), It.IsAny<CancellationToken>()))
            .Callback(() => failedJobCompletionSource.SetResult(true))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();

        services.AddScoped(_ => failedJobStoreMock.Object);

        var serviceProvider =services.BuildServiceProvider();

        var retryOptions = Options.Create(
            new BackgroundRetryOptions
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(10),
                JitterFactor = 0,
                MaxDelay = TimeSpan.FromMilliseconds(20)
            });

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance,
            retryOptions);

        var attempts = 0;

        await queue.QueueAsync(
              new BackgroundWorkItem(
                "TestJobType",
                "TestPayload",
                (_, _) =>
                {
                    attempts++;
                    return ValueTask.FromException(new TransientBackgroundException("Still failing"));
                }));

        await worker.StartAsync(CancellationToken.None);
        await failedJobCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, attempts);

        failedJobStoreMock.Verify(x => x.SaveAsync(
            It.Is<FailedBackgroundJob>(job => job.JobType == "TestJobType" && job.Payload == "TestPayload" && job.AttemptCount == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_WhenFailureIsPermanent_DoesNotRetry()
    {
        var queue = new BackgroundTaskQueue();
        var failedJobStoreMock = new Mock<IFailedBackgroundJobStore>();

        var services = new ServiceCollection();
        services.AddScoped(_ => failedJobStoreMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var retryOptions = Options.Create(
            new BackgroundRetryOptions
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(5),
                MaxDelay = TimeSpan.FromMilliseconds(20),
                JitterFactor = 0
            });

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance,
            retryOptions);

        var attempts = 0;

        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(new BackgroundWorkItem("TestJobType", "TestPayload", (_, _) =>
            {
                attempts++;

                completed.SetResult(true);

                return ValueTask.FromException(new PermanentBackgroundException("This will never succeed."));
            }));

        await worker.StartAsync(CancellationToken.None);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(5);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, attempts);

        failedJobStoreMock.Verify(x => x.SaveAsync(
            It.Is<FailedBackgroundJob>(c => c.Payload == "TestPayload" && c.AttemptCount == 1 && c.JobType == "TestJobType"),
            It.IsAny<CancellationToken>())
        , Times.Once);
    }

    [Fact]
    public async Task Worker_WhenFailureIsUnknownException_DoesNotRetry()
    {
        var queue = new BackgroundTaskQueue();

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var retryOptions = Options.Create(
            new BackgroundRetryOptions
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromMilliseconds(5),
                MaxDelay = TimeSpan.FromMilliseconds(20),
                JitterFactor = 0
            });

        var worker = new QueuedBackgroundService(
            queue,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedBackgroundService>.Instance,
            retryOptions);

        var attempts = 0;

        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(new BackgroundWorkItem("TestJobType", "TestPayload", (_, _) =>
        {
            attempts++;

            completed.SetResult(true);

            return ValueTask.FromException(new ArgumentNullException("This will never retry."));
        }));

        await worker.StartAsync(CancellationToken.None);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, attempts);
    }

    private sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
