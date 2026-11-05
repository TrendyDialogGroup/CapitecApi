using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Interfaces;
using TransactionAggregator.Infrastructure.Categorization;

namespace TransactionAggregator.Tests.Unit.Infrastructure;

public sealed class CategorizationServiceTests
{
    private static CategorizationService CreateService(ICategorizationRuleRepository rulesRepo)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => rulesRepo);
        services.AddMemoryCache();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();

        var dbStrategy = new DatabaseRuleCategorizationStrategy(
            scopeFactory,
            memoryCache,
            NullLogger<DatabaseRuleCategorizationStrategy>.Instance);

        return new CategorizationService([dbStrategy], NullLogger<CategorizationService>.Instance);
    }

    private static CategorizationRule CreateRule(string keyword, Category category, int priority)
    {
        var rule = CategorizationRule.Create(keyword, category.Id, priority);
        typeof(CategorizationRule)
            .GetProperty(nameof(CategorizationRule.Category))!
            .SetValue(rule, category);
        return rule;
    }

    [Fact]
    public async Task CategorizeAsync_WithEmptyRules_ShouldReturnNull()
    {
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo.Setup(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService(rulesRepo.Object).CategorizeAsync("Unknown Merchant");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CategorizeAsync_WhenMerchantMatchesKeyword_ShouldReturnCategoryId()
    {
        var groceries = Category.Create("Groceries");
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo
            .Setup(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateRule("checkers", groceries, priority: 10)]);

        var result = await CreateService(rulesRepo.Object).CategorizeAsync("Checkers Hyper Sandton");

        result.Should().Be(groceries.Id);
    }

    [Fact]
    public async Task CategorizeAsync_WhenMultipleRulesMatch_ShouldPreferHigherPriority()
    {
        var shopping = Category.Create("Shopping");
        var groceries = Category.Create("Groceries");
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo
            .Setup(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateRule("woolworths", shopping, priority: 5),
                CreateRule("woolworths food", groceries, priority: 10)
            ]);

        var result = await CreateService(rulesRepo.Object).CategorizeAsync("Woolworths Food Claremont");

        result.Should().Be(groceries.Id);
    }

    [Fact]
    public async Task RefreshRulesAsync_ShouldCallRepository()
    {
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo.Setup(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await CreateService(rulesRepo.Object).RefreshRulesAsync();

        rulesRepo.Verify(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategorizeAsync_WhenCalledTwice_ShouldUseCachedRules()
    {
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo.Setup(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var service = CreateService(rulesRepo.Object);
        await service.CategorizeAsync("Checkers");
        await service.CategorizeAsync("Uber");

        rulesRepo.Verify(r => r.GetActiveRulesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategorizeAsync_StrategiesOrderedByPriority_ShouldStopAtFirstMatch()
    {
        var firstCategory = Guid.NewGuid();
        var highPriority = new Mock<ICategorizationStrategy>();
        highPriority.SetupGet(s => s.Priority).Returns(200);
        highPriority
            .Setup(s => s.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstCategory);

        var lowPriority = new Mock<ICategorizationStrategy>();
        lowPriority.SetupGet(s => s.Priority).Returns(1);

        var service = new CategorizationService(
            [lowPriority.Object, highPriority.Object],
            NullLogger<CategorizationService>.Instance);

        var result = await service.CategorizeAsync("Any Merchant");

        result.Should().Be(firstCategory);
        lowPriority.Verify(
            s => s.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
