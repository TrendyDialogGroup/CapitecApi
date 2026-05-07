using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Interfaces;

namespace TransactionAggregator.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ITransactionRepository.
/// Uses Include() for eager-loading to prevent N+1 queries.
/// </summary>
public sealed class TransactionRepository(ApplicationDbContext context) : ITransactionRepository
{
    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Transaction?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        => await context.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.ExternalId == externalId, ct);

    public async Task<bool> ExistsByExternalIdAsync(string externalId, CancellationToken ct = default)
        => await context.Transactions
            .AnyAsync(t => t.ExternalId == externalId, ct);

    public async Task<IReadOnlyList<string>> GetExistingExternalIdsAsync(IEnumerable<string> externalIds, CancellationToken ct = default)
    {
        var idsList = externalIds.ToList();
        if (idsList.Count == 0) return [];

        return await context.Transactions
            .Where(t => idsList.Contains(t.ExternalId))
            .Select(t => t.ExternalId)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedAsync(
        TransactionFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.Transactions
            .Include(t => t.Category)
            .AsNoTracking()
            .AsQueryable();

        // Apply filters
        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= filter.DateTo.Value);
        if (filter.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);
        if (filter.MinAmount.HasValue)
            query = query.Where(t => t.Amount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue)
            query = query.Where(t => t.Amount <= filter.MaxAmount.Value);
        if (filter.Source.HasValue)
            query = query.Where(t => t.Source == filter.Source.Value);
        if (!string.IsNullOrWhiteSpace(filter.MerchantName))
            query = query.Where(t => t.MerchantName.Contains(filter.MerchantName));

        // Count before paging — one round-trip using EF split query
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.AsReadOnly(), totalCount);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
        => await context.Transactions.AddAsync(transaction, ct);

    public async Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken ct = default)
        => await context.Transactions.AddRangeAsync(transactions, ct);

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        context.Transactions.Update(transaction);
        return Task.CompletedTask;
    }
}

public sealed class CategoryRepository(ApplicationDbContext context) : ICategoryRepository
{
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Categories.FindAsync([id], ct);

    public async Task<Category?> GetByNameAsync(string name, CancellationToken ct = default)
        => await context.Categories
            .FirstOrDefaultAsync(c => c.NormalizedName == name.ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
        => await context.Categories.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(Category category, CancellationToken ct = default)
        => await context.Categories.AddAsync(category, ct);
}

public sealed class CategorizationRuleRepository(ApplicationDbContext context) : ICategorizationRuleRepository
{
    public async Task<IReadOnlyList<CategorizationRule>> GetActiveRulesAsync(CancellationToken ct = default)
        => await context.CategorizationRules
            .Include(r => r.Category)
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CategorizationRule>> GetAllAsync(CancellationToken ct = default)
        => await context.CategorizationRules
            .Include(r => r.Category)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(CategorizationRule rule, CancellationToken ct = default)
        => await context.CategorizationRules.AddAsync(rule, ct);
}
