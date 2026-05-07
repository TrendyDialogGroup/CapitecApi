using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Infrastructure.Persistence.Configurations;

namespace TransactionAggregator.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-set UpdatedAt on modified entities
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Property(nameof(Domain.Common.BaseEntity.UpdatedAt)).CurrentValue = DateTime.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
