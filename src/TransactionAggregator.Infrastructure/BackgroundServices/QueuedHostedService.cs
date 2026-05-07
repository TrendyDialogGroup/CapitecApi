using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionAggregator.Application.Common.Interfaces;

namespace TransactionAggregator.Infrastructure.BackgroundServices;

/// <summary>
/// Long-running background service that dequeues and executes queued background work items.
/// Creates a new dependency injection scope for each execution.
/// </summary>
public sealed class QueuedHostedService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queued Hosted Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);

                // Execute the work item in its own scope so scoped services (like DbContext) work correctly
                using var scope = scopeFactory.CreateScope();
                
                try
                {
                    logger.LogDebug("Executing background task.");
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred executing background work item.");
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stopping token was signaled
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred dequeuing a background work item.");
            }
        }

        logger.LogInformation("Queued Hosted Service is stopping.");
    }
}
