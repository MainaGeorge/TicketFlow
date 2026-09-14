using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketFlow.Application.Background;

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

    private async Task ExecuteWithRetryAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem, CancellationToken cancellationToken)
    {
        for(var attempt = 1; attempt <= _retryOptions.MaxAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                await workItem(scope.ServiceProvider, cancellationToken);

                return;
            }
            catch(OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch(Exception ex)
            {
                var delay = CalculateRetryDelay(attempt);

                logger.LogWarning(ex, "Background work item failed on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}.", attempt, _retryOptions.MaxAttempts, delay);

                await Task.Delay(delay, cancellationToken);

                if(attempt == _retryOptions.MaxAttempts)
                {
                    logger.LogError(ex, "Background work item failed after {MaxAttempts} attempts", _retryOptions.MaxAttempts);

                    return;
                }
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while executing a background work item.");
            }
        }

        logger.LogInformation("Queued background service stopped.");
    }
}
