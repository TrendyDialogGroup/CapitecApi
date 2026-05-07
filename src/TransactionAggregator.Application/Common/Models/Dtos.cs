using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Application.Common.Models;

// ── Transaction DTOs ─────────────────────────────────────────────────────────

public sealed class TransactionDto
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string MerchantName { get; init; } = null!;
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Source { get; init; } = null!;
    public bool IsCategorizedManually { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
}

// ── Aggregation DTOs ─────────────────────────────────────────────────────────

public sealed class SpendByCategoryDto
{
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = null!;
    public decimal TotalAmount { get; init; }
    public int TransactionCount { get; init; }
    public string Currency { get; init; } = null!;
}

public sealed class MonthlySummaryDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthName { get; init; } = null!;
    public decimal TotalSpend { get; init; }
    public int TransactionCount { get; init; }
    public IReadOnlyList<SpendByCategoryDto> ByCategory { get; init; } = [];
}

public sealed class TopMerchantDto
{
    public string MerchantName { get; init; } = null!;
    public decimal TotalAmount { get; init; }
    public int TransactionCount { get; init; }
    public string? CategoryName { get; init; }
}

// ── Ingestion DTOs ───────────────────────────────────────────────────────────

public sealed class IngestResultDto
{
    public int TotalReceived { get; init; }
    public int Ingested { get; init; }
    public int Duplicates { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
