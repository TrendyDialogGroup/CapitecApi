namespace TransactionAggregator.Domain.Interfaces;

public interface IUnitOfWork
{
    ITransactionRepository Transactions { get; }
    ICategoryRepository Categories { get; }
    ICategorizationRuleRepository CategorizationRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
