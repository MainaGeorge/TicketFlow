using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketFlow.Application.Background;
using TicketFlow.Domain.Background;

namespace TicketFlow.Infrastructure.Background;

public class QueuedBackgroundService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<QueuedBackgroundService> logger,
    IOptions<BackgroundRetryOptions> retryOptions) : BackgroundService
{
    private readonly BackgroundRetryOptions _retryOptions = retryOptions.Value;

    private TimeSpan CalculateRetryDelay(int attempt)
    {
        var exponentialDelayMs = _retryOptions.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var cappedDelayMs = Math.Min(exponentialDelayMs, _retryOptions.MaxDelay.TotalMilliseconds);
        var jitterRange = cappedDelayMs * _retryOptions.JitterFactor;
        var jitter = Random.Shared.NextDouble() * jitterRange;

        return TimeSpan.FromMilliseconds(cappedDelayMs + jitter);
    }

    private async Task StoreFailedJobAsync(BackgroundWorkItem workItem, Exception exception, int attemptCount, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IFailedBackgroundJobStore>();

        var failedJob = new FailedBackgroundJob
        {
            JobType = workItem.JobType,
            Payload = workItem.Payload,
            FailureReason = exception.Message,
            ExceptionType = exception.GetType().FullName,
            AttemptCount = attemptCount,
            FailedAt = DateTimeOffset.UtcNow
        };

        await store.SaveAsync(failedJob, cancellationToken);
    }

    private async Task ExecuteWithRetryAsync(BackgroundWorkItem workItem, CancellationToken cancellationToken)
    {
        for(var attempt = 1; attempt <= _retryOptions.MaxAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                await workItem.ExecuteAsync(scope.ServiceProvider, cancellationToken);

                return;
            }
            catch(OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (PermanentBackgroundException ex)
            {
                logger.LogError(ex, "Background work item failed permanently. No retry will be attempted.");

                await StoreFailedJobAsync(workItem, ex, attempt, cancellationToken);

                return;
            }
            catch (TransientBackgroundException ex)
            {
                var delay = CalculateRetryDelay(attempt);

                logger.LogWarning(ex, "Background work item failed on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}.", attempt, _retryOptions.MaxAttempts, delay);

                await Task.Delay(delay, cancellationToken);

                if(attempt == _retryOptions.MaxAttempts)
                {
                    logger.LogError(ex, "Background work item failed after {MaxAttempts} attempts", _retryOptions.MaxAttempts);

                    await StoreFailedJobAsync(workItem, ex, attempt, cancellationToken);

                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected background work item failure. No retry will be attempted.");

                await StoreFailedJobAsync(workItem, ex, attempt, cancellationToken);

                return;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queued background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);
                await ExecuteWithRetryAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shut down the application, this should not be logged as an error.
                logger.LogInformation("Background service shutting down gracefully...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while executing a background work item.");
            }
        }

        logger.LogInformation("Queued background service stopped.");
    }
}
