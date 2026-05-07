using TransactionAggregator.Domain.Interfaces;

namespace TransactionAggregator.Infrastructure.Persistence;

public sealed class UnitOfWork(
    ApplicationDbContext context,
    ITransactionRepository transactions,
    ICategoryRepository categories,
    ICategorizationRuleRepository categorizationRules)
    : IUnitOfWork
{
    public ITransactionRepository Transactions => transactions;
    public ICategoryRepository Categories => categories;
    public ICategorizationRuleRepository CategorizationRules => categorizationRules;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
