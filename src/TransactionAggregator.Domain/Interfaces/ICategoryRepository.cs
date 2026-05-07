using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
}

public interface ICategorizationRuleRepository
{
    Task<IReadOnlyList<CategorizationRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategorizationRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(CategorizationRule rule, CancellationToken cancellationToken = default);
}
