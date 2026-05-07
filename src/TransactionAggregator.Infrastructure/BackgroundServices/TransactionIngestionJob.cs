using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregator.Application.Transactions.Commands;

namespace TransactionAggregator.Infrastructure.BackgroundServices;

/// <summary>
/// Periodic background service that triggers transaction ingestion from all configured sources.
/// Runs on a configurable interval — default 5 minutes.
/// </summary>
public sealed class TransactionIngestionJob(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionJobOptions> options,
    ILogger<TransactionIngestionJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "TransactionIngestionJob started — interval: {IntervalSeconds}s",
            options.Value.IntervalSeconds);

        // Initial delay to let the app fully start before first run
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIngestionAsync(stoppingToken);

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.Value.IntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("TransactionIngestionJob stopping.");
    }

    private async Task RunIngestionAsync(CancellationToken ct)
    {
        logger.LogInformation("[IngestionJob] Starting ingestion cycle at {Time}", DateTime.UtcNow);

        try
        {
            // Must create a new scope — background services are singletons but DbContext is scoped
            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new IngestTransactionsCommand(), ct);

            logger.LogInformation(
                "[IngestionJob] Cycle complete — Received: {Total}, Ingested: {Ingested}, Duplicates: {Dupes}, Failed: {Failed}",
                result.TotalReceived, result.Ingested, result.Duplicates, result.Failed);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "[IngestionJob] Ingestion cycle failed");
        }
    }
}

public sealed class IngestionJobOptions
{
    public const string SectionName = "IngestionJob";

    /// <summary>How often to poll data sources, in seconds.</summary>
    public int IntervalSeconds { get; init; } = 300;

    public bool Enabled { get; init; } = true;
}
