using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Domain.Interfaces;
using TransactionAggregator.Infrastructure.Persistence;

namespace TransactionAggregator.Tests.Integration;

public sealed class TransactionRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TransactionRepository _repository;

    public TransactionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"RepoTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _repository = new TransactionRepository(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task GetExistingExternalIdsAsync_ShouldReturnOnlyKnownIds()
    {
        var existing = Transaction.Create("EXT-1", 10m, "ZAR", "Checkers", DateTime.UtcNow, TransactionSource.RestApi);
        await _repository.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var result = await _repository.GetExistingExternalIdsAsync(["EXT-1", "EXT-MISSING"]);

        result.Should().Equal("EXT-1");
    }

    [Fact]
    public async Task GetPagedAsync_ShouldFilterByMerchantAndPage()
    {
        await _repository.AddRangeAsync(
        [
            Transaction.Create("P1", 10m, "ZAR", "Checkers", DateTime.UtcNow.AddMinutes(-2), TransactionSource.RestApi),
            Transaction.Create("P2", 20m, "ZAR", "Uber", DateTime.UtcNow.AddMinutes(-1), TransactionSource.RestApi),
            Transaction.Create("P3", 30m, "ZAR", "Checkers Hyper", DateTime.UtcNow, TransactionSource.FileImport)
        ]);
        await _dbContext.SaveChangesAsync();

        var (items, total) = await _repository.GetPagedAsync(
            new TransactionFilter { MerchantName = "Checkers" },
            page: 1,
            pageSize: 10);

        total.Should().Be(2);
        items.Should().OnlyContain(t => t.MerchantName.Contains("Checkers", StringComparison.OrdinalIgnoreCase) || t.MerchantName.Contains("Checkers"));
        items.Select(t => t.ExternalId).Should().BeEquivalentTo(["P1", "P3"]);
    }

    [Fact]
    public async Task ExistsByExternalIdAsync_ShouldReflectPersistence()
    {
        var tx = Transaction.Create("EXISTS-1", 10m, "ZAR", "Uber", DateTime.UtcNow, TransactionSource.RestApi);
        await _repository.AddAsync(tx);
        await _dbContext.SaveChangesAsync();

        (await _repository.ExistsByExternalIdAsync("EXISTS-1")).Should().BeTrue();
        (await _repository.ExistsByExternalIdAsync("NOPE")).Should().BeFalse();
    }
}
