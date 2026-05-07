using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Transaction> Transactions { get; }
    DbSet<Category> Categories { get; }
    DbSet<CategorizationRule> CategorizationRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
