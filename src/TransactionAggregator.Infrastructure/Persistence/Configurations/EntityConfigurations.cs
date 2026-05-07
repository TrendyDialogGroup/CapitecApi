using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever(); // Domain sets it

        builder.Property(t => t.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        // Unique index for idempotency — prevents duplicates at DB level
        builder.HasIndex(t => t.ExternalId)
            .IsUnique()
            .HasDatabaseName("IX_Transactions_ExternalId");

        builder.Property(t => t.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(t => t.MerchantName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.Source)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired(false);

        builder.Property(t => t.IsCategorizedManually)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationship: nullable FK to Category
        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Performance indexes
        builder.HasIndex(t => t.TransactionDate)
            .HasDatabaseName("IX_Transactions_TransactionDate");

        builder.HasIndex(t => t.CategoryId)
            .HasDatabaseName("IX_Transactions_CategoryId");

        builder.HasIndex(t => new { t.TransactionDate, t.CategoryId })
            .HasDatabaseName("IX_Transactions_Date_Category");
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(c => c.NormalizedName)
            .IsUnique()
            .HasDatabaseName("IX_Categories_NormalizedName");

        builder.Property(c => c.Description)
            .HasMaxLength(500);
    }
}

public sealed class CategorizationRuleConfiguration : IEntityTypeConfiguration<CategorizationRule>
{
    public void Configure(EntityTypeBuilder<CategorizationRule> builder)
    {
        builder.ToTable("CategorizationRules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Keyword)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.Keyword)
            .HasDatabaseName("IX_CategorizationRules_Keyword");
    }
}
