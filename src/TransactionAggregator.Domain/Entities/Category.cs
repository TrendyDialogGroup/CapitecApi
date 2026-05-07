using TransactionAggregator.Domain.Common;

namespace TransactionAggregator.Domain.Entities;

public sealed class Category : BaseEntity
{
    private Category() { } // EF Core

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string NormalizedName { get; private set; } = null!;

    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();
    private readonly List<Transaction> _transactions = new();

    public static Category Create(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Category
        {
            Name = name.Trim(),
            NormalizedName = name.Trim().ToUpperInvariant(),
            Description = description
        };
    }
}
