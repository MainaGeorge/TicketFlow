using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Background;

namespace TicketFlow.Infrastructure.Background;

public class QueuedBackgroundService(IBackgroundTaskQueue taskQueue, IServiceScopeFactory scopeFactory, ILogger<QueuedBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queued background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();

                await workItem(scope.ServiceProvider, stoppingToken);
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
