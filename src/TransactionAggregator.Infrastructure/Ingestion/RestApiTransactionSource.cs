using Microsoft.Extensions.Logging;
using Polly.Registry;
using System.Net.Http.Json;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Infrastructure.Ingestion;

public sealed class RestApiTransactionSource(
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<RestApiTransactionSource> logger) : ITransactionDataSource
{
    public string SourceName => "RestApiGateway";
    public TransactionSource SourceType => TransactionSource.RestApi;

    public async Task<IReadOnlyList<RawTransaction>> FetchAsync(CancellationToken ct = default)
    {
        var pipeline = pipelineProvider.GetPipeline("ExternalSource");

        return await pipeline.ExecuteAsync(async token =>
        {
            logger.LogInformation("Fetching transactions from REST API source...");

            // Simulate network delay and potential transient failure
            await Task.Delay(50, token);
            
            // Randomly simulate a network error to trigger Polly retry
            if (Random.Shared.Next(0, 3) == 0)
            {
                logger.LogWarning("Simulated transient network failure in REST API.");
                throw new HttpRequestException("Simulated 503 Service Unavailable");
            }

            // Mock data — represents what the API would return
            var transactions = GenerateMockTransactions();
            logger.LogInformation("REST API source returned {Count} transactions", transactions.Count);
            return transactions;
        }, ct);
    }

    private static List<RawTransaction> GenerateMockTransactions()
    {
        var rng = new Random();
        var merchants = new[]
        {
            ("Uber", 15m, 80m),
            ("Checkers", 50m, 800m),
            ("McDonald's", 30m, 120m),
            ("Netflix", 169m, 169m),
            ("Woolworths Food", 200m, 900m),
            ("Bolt", 20m, 60m),
            ("KFC", 45m, 150m),
            ("Engen Petrol", 400m, 900m),
            ("Dischem", 80m, 400m),
            ("Takealot", 100m, 2000m),
        };

        var now = DateTime.UtcNow;
        return Enumerable.Range(0, rng.Next(15, 30))
            .Select(i =>
            {
                var (merchant, min, max) = merchants[rng.Next(merchants.Length)];
                return new RawTransaction
                {
                    ExternalId = $"REST-{Guid.NewGuid():N}",
                    Amount = Math.Round((decimal)(rng.NextDouble() * (double)(max - min) + (double)min), 2),
                    Currency = "ZAR",
                    MerchantName = merchant,
                    TransactionDate = now.AddDays(-rng.Next(0, 30))
                };
            })
            .ToList();
    }
}
