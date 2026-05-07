using TransactionAggregator.Domain.Common;

namespace TransactionAggregator.Domain.Entities;

public sealed class CategorizationRule : BaseEntity
{
    private CategorizationRule() { } // EF Core
    public string Keyword { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public int Priority { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static CategorizationRule Create(string keyword, Guid categoryId, int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        return new CategorizationRule
        {
            Keyword = keyword.Trim().ToLowerInvariant(),
            CategoryId = categoryId,
            Priority = priority,
            IsActive = true
        };
    }
}
