using FluentAssertions;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Tests.Unit.Domain;

public sealed class TransactionTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnTransaction()
    {
        // Act
        var tx = Transaction.Create("EXT-001", 250.00m, "ZAR", "Checkers", DateTime.UtcNow, TransactionSource.RestApi);

        // Assert
        tx.Should().NotBeNull();
        tx.Id.Should().NotBeEmpty();
        tx.ExternalId.Should().Be("EXT-001");
        tx.Amount.Should().Be(250.00m);
        tx.Currency.Should().Be("ZAR");
        tx.MerchantName.Should().Be("Checkers");
        tx.CategoryId.Should().BeNull();
        tx.IsCategorizedManually.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => Transaction.Create("EXT-001", -100m, "ZAR", "Test", DateTime.UtcNow, TransactionSource.RestApi);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidExternalId_ShouldThrowArgumentException(string? externalId)
    {
        // Act & Assert
        var act = () => Transaction.Create(externalId!, 100m, "ZAR", "Test", DateTime.UtcNow, TransactionSource.RestApi);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Categorize_ShouldSetCategoryId()
    {
        // Arrange
        var tx = Transaction.Create("EXT-001", 100m, "ZAR", "Uber", DateTime.UtcNow, TransactionSource.RestApi);
        var categoryId = Guid.NewGuid();

        // Act
        tx.Categorize(categoryId);

        // Assert
        tx.CategoryId.Should().Be(categoryId);
        tx.IsCategorizedManually.Should().BeFalse();
        tx.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Categorize_ManualFlag_ShouldSetManuallyTrue()
    {
        // Arrange
        var tx = Transaction.Create("EXT-001", 100m, "ZAR", "Uber", DateTime.UtcNow, TransactionSource.RestApi);
        var categoryId = Guid.NewGuid();

        // Act
        tx.Categorize(categoryId, manual: true);

        // Assert
        tx.IsCategorizedManually.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseTransactionCreatedEvent()
    {
        // Act
        var tx = Transaction.Create("EXT-001", 100m, "ZAR", "Test", DateTime.UtcNow, TransactionSource.FileImport);

        // Assert
        tx.DomainEvents.Should().ContainSingle(e => e is TransactionCreatedEvent);
        var evt = (TransactionCreatedEvent)tx.DomainEvents.First();
        evt.ExternalId.Should().Be("EXT-001");
        evt.Source.Should().Be(TransactionSource.FileImport);
    }

    [Fact]
    public void ClearCategory_ShouldResetCategoryState()
    {
        // Arrange
        var tx = Transaction.Create("EXT-001", 100m, "ZAR", "Test", DateTime.UtcNow, TransactionSource.RestApi);
        tx.Categorize(Guid.NewGuid(), manual: true);

        // Act
        tx.ClearCategory();

        // Assert
        tx.CategoryId.Should().BeNull();
        tx.IsCategorizedManually.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUpperCase()
    {
        // Act
        var tx = Transaction.Create("EXT-001", 100m, "zar", "Test", DateTime.UtcNow, TransactionSource.RestApi);

        // Assert
        tx.Currency.Should().Be("ZAR");
    }

    [Fact]
    public void Create_ShouldTrimMerchantName()
    {
        // Act
        var tx = Transaction.Create("EXT-001", 100m, "ZAR", "  Checkers  ", DateTime.UtcNow, TransactionSource.RestApi);

        // Assert
        tx.MerchantName.Should().Be("Checkers");
    }
}
