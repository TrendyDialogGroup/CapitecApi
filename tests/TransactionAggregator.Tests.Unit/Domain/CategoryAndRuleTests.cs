using FluentAssertions;
using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Tests.Unit.Domain;

public sealed class CategoryAndRuleTests
{
    [Fact]
    public void Category_Create_ShouldNormalizeName()
    {
        var category = Category.Create("  Groceries  ", "Food stores");

        category.Name.Should().Be("Groceries");
        category.NormalizedName.Should().Be("GROCERIES");
        category.Description.Should().Be("Food stores");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Category_Create_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => Category.Create(name!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CategorizationRule_Create_ShouldNormalizeKeyword()
    {
        var categoryId = Guid.NewGuid();
        var rule = CategorizationRule.Create("  Checkers  ", categoryId, priority: 7);

        rule.Keyword.Should().Be("checkers");
        rule.CategoryId.Should().Be(categoryId);
        rule.Priority.Should().Be(7);
        rule.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CategorizationRule_Create_WithInvalidKeyword_ShouldThrow(string? keyword)
    {
        var act = () => CategorizationRule.Create(keyword!, Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }
}
