namespace TransactionAggregator.Application.Common.Interfaces;
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);


    Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Cache key constants — centralised to prevent typos.</summary>
public static class CacheKeys
{
    public const string TotalSpendByCategory = "aggregations:spend_by_category";
    public const string TopMerchants = "aggregations:top_merchants";
    public const string MonthlySummaryPrefix = "aggregations:monthly_summary:";
    public const string Categories = "categories:all";

    public static string MonthlySummary(int year, int month) =>
        $"{MonthlySummaryPrefix}{year}:{month:D2}";
}
