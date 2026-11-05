using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Application.Aggregations.Queries;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Infrastructure.Persistence;
using TransactionAggregator.Tests.Unit.Fakes;

namespace TransactionAggregator.Tests.Unit.Application;

public sealed class AggregationQueryHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly InMemoryCacheService _cache = new();

    public AggregationQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private async Task<(Category groceries, Category transport)> SeedAsync()
    {
        var groceries = Category.Create("Groceries");
        var transport = Category.Create("Transport");
        _dbContext.Categories.AddRange(groceries, transport);

        var checkers = Transaction.Create(
            "A1", 100m, "ZAR", "Checkers",
            new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            TransactionSource.RestApi);
        checkers.Categorize(groceries.Id);

        var uber = Transaction.Create(
            "A2", 50m, "ZAR", "Uber",
            new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            TransactionSource.RestApi);
        uber.Categorize(transport.Id);

        var anotherCheckers = Transaction.Create(
            "A3", 25m, "ZAR", "Checkers",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            TransactionSource.FileImport);
        anotherCheckers.Categorize(groceries.Id);

        _dbContext.Transactions.AddRange(checkers, uber, anotherCheckers);
        await _dbContext.SaveChangesAsync();
        return (groceries, transport);
    }

    [Fact]
    public async Task GetTotalSpendByCategory_ShouldAggregateAtDatabase()
    {
        var (groceries, _) = await SeedAsync();
        var handler = new GetTotalSpendByCategoryQueryHandler(_dbContext, _cache);

        var result = await handler.Handle(new GetTotalSpendByCategoryQuery(), default);

        result.Should().HaveCount(2);
        result.First().CategoryId.Should().Be(groceries.Id);
        result.First().TotalAmount.Should().Be(125m);
        result.First().TransactionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTotalSpendByCategory_WithDateFilter_ShouldLimitResults()
    {
        await SeedAsync();
        var handler = new GetTotalSpendByCategoryQueryHandler(_dbContext, _cache);

        var result = await handler.Handle(new GetTotalSpendByCategoryQuery(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc)), default);

        result.Sum(x => x.TotalAmount).Should().Be(150m);
        result.Sum(x => x.TransactionCount).Should().Be(2);
    }

    [Fact]
    public async Task GetMonthlySummary_ShouldReturnMonthTotals()
    {
        await SeedAsync();
        var handler = new GetMonthlySummaryQueryHandler(_dbContext, _cache);

        var result = await handler.Handle(new GetMonthlySummaryQuery(2026, 3), default);

        result.Year.Should().Be(2026);
        result.Month.Should().Be(3);
        result.TotalSpend.Should().Be(150m);
        result.TransactionCount.Should().Be(2);
        result.ByCategory.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTopMerchants_ShouldOrderBySpendAndRespectTake()
    {
        await SeedAsync();
        var handler = new GetTopMerchantsQueryHandler(_dbContext, _cache);

        var result = await handler.Handle(new GetTopMerchantsQuery(1), default);

        result.Should().ContainSingle();
        result[0].MerchantName.Should().Be("Checkers");
        result[0].TotalAmount.Should().Be(125m);
    }

    [Fact]
    public async Task GetTotalSpendByCategory_ShouldUseFilterAwareCacheKey()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var handler = new GetTotalSpendByCategoryQueryHandler(_dbContext, _cache);

        await handler.Handle(new GetTotalSpendByCategoryQuery(from, null), default);

        _cache.Keys.Should().Contain(CacheKeys.TotalSpendByCategory(from, null));
    }

    [Fact]
    public async Task GetTopMerchants_ShouldCachePerTakeValue()
    {
        await SeedAsync();
        var handler = new GetTopMerchantsQueryHandler(_dbContext, _cache);

        await handler.Handle(new GetTopMerchantsQuery(1), default);
        await handler.Handle(new GetTopMerchantsQuery(5), default);

        _cache.Keys.Should().Contain(CacheKeys.TopMerchants(1));
        _cache.Keys.Should().Contain(CacheKeys.TopMerchants(5));
    }
}
