using TransactionAggregator.Domain.Common;
using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Domain.Entities;

public sealed class Transaction : BaseEntity
{
    private Transaction() { } // EF Core
    public string ExternalId { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;

    public string MerchantName { get; private set; } = null!;

    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }

    public DateTime TransactionDate { get; private set; }

    public TransactionSource Source { get; private set; }

    /// <summary>Indicates whether the transaction has been manually categorised.</summary>
    public bool IsCategorizedManually { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────

    public static Transaction Create(
        string externalId,
        decimal amount,
        string currency,
        string merchantName,
        DateTime transactionDate,
        TransactionSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantName);

        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount cannot be negative.");

        var transaction = new Transaction
        {
            ExternalId = externalId.Trim(),
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            MerchantName = merchantName.Trim(),
            TransactionDate = transactionDate.ToUniversalTime(),
            Source = source
        };

        transaction.AddDomainEvent(new TransactionCreatedEvent(transaction.Id, externalId, source));
        return transaction;
    }

    // ── Behaviour ────────────────────────────────────────────────────────

    public void Categorize(Guid categoryId, bool manual = false)
    {
        CategoryId = categoryId;
        IsCategorizedManually = manual;
        SetUpdatedAt();
    }

    public void ClearCategory()
    {
        CategoryId = null;
        IsCategorizedManually = false;
        SetUpdatedAt();
    }
}

// ── Domain Events ────────────────────────────────────────────────────────────

public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    string ExternalId,
    TransactionSource Source) : IDomainEvent;
