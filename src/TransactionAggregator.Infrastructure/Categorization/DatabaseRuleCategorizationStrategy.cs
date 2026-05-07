using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransactionAggregator.Application.Common.Interfaces;

namespace TransactionAggregator.Infrastructure.Categorization;

public sealed class DatabaseRuleCategorizationStrategy(
    IServiceScopeFactory scopeFactory,
    IMemoryCache memoryCache,
    ILogger<DatabaseRuleCategorizationStrategy> logger) : ICategorizationStrategy
{
    private const string RulesCacheKey = "CategorizationRules";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    // This strategy runs first
    public int Priority => 100;

    public async Task<Guid?> CategorizeAsync(string merchantName, CancellationToken ct = default)
    {
        var rules = await GetCachedRulesAsync(ct);

        var normalized = merchantName.ToLowerInvariant();

        var match = rules
            .OrderByDescending(r => r.Priority)
            .FirstOrDefault(r => normalized.Contains(r.Keyword));

        if (match is not null)
        {
            logger.LogDebug("Merchant '{Merchant}' matched DB rule '{Keyword}' → Category '{Category}'",
                merchantName, match.Keyword, match.CategoryName);
            return match.CategoryId;
        }

        return null;
    }

    public async Task RefreshRulesAsync(CancellationToken ct = default)
    {
        memoryCache.Remove(RulesCacheKey);
        await GetCachedRulesAsync(ct);
    }

    private async Task<IReadOnlyList<RuleEntry>> GetCachedRulesAsync(CancellationToken ct)
    {
        if (memoryCache.TryGetValue(RulesCacheKey, out IReadOnlyList<RuleEntry>? rules) && rules is not null)
            return rules;

        await _lock.WaitAsync(ct);
        try
        {
            if (memoryCache.TryGetValue(RulesCacheKey, out rules) && rules is not null)
                return rules;

            using var scope = scopeFactory.CreateScope();
            var rulesRepository = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.ICategorizationRuleRepository>();

            var dbRules = await rulesRepository.GetActiveRulesAsync(ct);
            rules = dbRules.Select(r => new RuleEntry(r.Keyword, r.CategoryId, r.Category.Name, r.Priority)).ToList();
            
            memoryCache.Set(RulesCacheKey, rules, CacheLifetime);
            logger.LogInformation("Database Categorization rules refreshed — {Count} active rules loaded into memory cache", rules.Count);
            
            return rules;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record RuleEntry(string Keyword, Guid CategoryId, string CategoryName, int Priority);
}
