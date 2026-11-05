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

/// <summary>Cache key constants — centralised to prevent typos and include filter dimensions.</summary>
public static class CacheKeys
{
    public const string AggregationsPrefix = "aggregations:";
    public const string Categories = "categories:all";

    public static string TotalSpendByCategory(DateTime? dateFrom, DateTime? dateTo) =>
        $"{AggregationsPrefix}spend_by_category:{Format(dateFrom)}:{Format(dateTo)}";

    public static string TopMerchants(int take) =>
        $"{AggregationsPrefix}top_merchants:{take}";

    public static string MonthlySummary(int year, int month) =>
        $"{AggregationsPrefix}monthly_summary:{year}:{month:D2}";

    private static string Format(DateTime? value) =>
        value?.ToUniversalTime().ToString("O") ?? "all";
}
