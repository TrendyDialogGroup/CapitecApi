using Microsoft.Extensions.Logging;
using TransactionAggregator.Application.Common.Interfaces;

namespace TransactionAggregator.Infrastructure.Categorization;

/// <summary>
/// Core categorization engine.
/// Delegates categorization to a pipeline of ICategorizationStrategy implementations.
/// </summary>
public sealed class CategorizationService(
    IEnumerable<ICategorizationStrategy> strategies,
    ILogger<CategorizationService> logger) : ICategorizationService
{
    private readonly IReadOnlyList<ICategorizationStrategy> _strategies = strategies.OrderByDescending(s => s.Priority).ToList();

    public async Task<Guid?> CategorizeAsync(string merchantName, CancellationToken ct = default)
    {
        foreach (var strategy in _strategies)
        {
            var result = await strategy.CategorizeAsync(merchantName, ct);
            if (result.HasValue)
            {
                logger.LogDebug("Categorized '{Merchant}' using strategy {StrategyType}", merchantName, strategy.GetType().Name);
                return result;
            }
        }

        logger.LogDebug("No strategy could categorize merchant '{Merchant}'", merchantName);
        return null;
    }

    public async Task RefreshRulesAsync(CancellationToken ct = default)
    {
        foreach (var strategy in _strategies.OfType<DatabaseRuleCategorizationStrategy>())
        {
            await strategy.RefreshRulesAsync(ct);
        }
    }
}
