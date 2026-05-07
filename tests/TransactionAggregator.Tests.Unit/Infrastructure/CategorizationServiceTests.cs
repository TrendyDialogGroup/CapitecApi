using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TransactionAggregator.Domain.Interfaces;
using TransactionAggregator.Infrastructure.Categorization;

namespace TransactionAggregator.Tests.Unit.Infrastructure;

public sealed class CategorizationServiceTests
{
    private static CategorizationService CreateService(ICategorizationRuleRepository rulesRepo)
    {
        // Build a proper ServiceProvider with the scoped dependency
        var services = new ServiceCollection();
        services.AddScoped(_ => rulesRepo);
        services.AddMemoryCache();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var memoryCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();

        var dbStrategy = new DatabaseRuleCategorizationStrategy(scopeFactory, memoryCache, NullLogger<DatabaseRuleCategorizationStrategy>.Instance);

        return new CategorizationService([dbStrategy], NullLogger<CategorizationService>.Instance);
    }

    [Fact]
    public async Task CategorizeAsync_WithEmptyRules_ShouldReturnNull()
    {
        // Arrange
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo.Setup(r => r.GetActiveRulesAsync(default)).ReturnsAsync([]);

        var service = CreateService(rulesRepo.Object);

        // Act
        var result = await service.CategorizeAsync("Unknown Merchant", default);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshRulesAsync_ShouldCallRepository()
    {
        // Arrange
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo.Setup(r => r.GetActiveRulesAsync(default)).ReturnsAsync([]);

        var service = CreateService(rulesRepo.Object);

        // Act
        await service.RefreshRulesAsync(default);

        // Assert — repository was called during refresh
        rulesRepo.Verify(r => r.GetActiveRulesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CategorizeAsync_WhenCalledTwice_ShouldUseCachedRules()
    {
        // Arrange
        var rulesRepo = new Mock<ICategorizationRuleRepository>();
        rulesRepo.Setup(r => r.GetActiveRulesAsync(default)).ReturnsAsync([]);

        var service = CreateService(rulesRepo.Object);

        // Act — call twice
        await service.CategorizeAsync("Checkers", default);
        await service.CategorizeAsync("Uber", default);

        // Assert — repository only called once due to in-memory cache
        rulesRepo.Verify(r => r.GetActiveRulesAsync(default), Times.Once);
    }
}
