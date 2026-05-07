using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Domain.Interfaces;

/// <summary>
/// Repository contract for Transaction aggregate.
/// Follows the Repository pattern — only domain-meaningful operations.
/// </summary>
public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetExistingExternalIdsAsync(IEnumerable<string> externalIds, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedAsync(
        TransactionFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
}

/// <summary>Filter criteria for transaction queries.</summary>
public sealed class TransactionFilter
{
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public TransactionSource? Source { get; init; }
    public string? MerchantName { get; init; }
}
