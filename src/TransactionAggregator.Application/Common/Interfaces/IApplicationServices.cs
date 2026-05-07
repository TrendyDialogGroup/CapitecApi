using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Application.Common.Interfaces;

public interface ICategorizationService
{
    Task<Guid?> CategorizeAsync(string merchantName, CancellationToken cancellationToken = default);
    Task RefreshRulesAsync(CancellationToken cancellationToken = default);
}

public interface ICategorizationStrategy
{
    int Priority { get; }
    Task<Guid?> CategorizeAsync(string merchantName, CancellationToken cancellationToken = default);
}


public interface ITransactionDataSource
{
    string SourceName { get; }
    TransactionSource SourceType { get; }

    Task<IReadOnlyList<RawTransaction>> FetchAsync(CancellationToken cancellationToken = default);
}


public sealed class RawTransaction
{
    public required string ExternalId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string MerchantName { get; init; }
    public required DateTime TransactionDate { get; init; }
}

/// <summary>JWT token generation service.</summary>
public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string email, IEnumerable<string> roles);
}
