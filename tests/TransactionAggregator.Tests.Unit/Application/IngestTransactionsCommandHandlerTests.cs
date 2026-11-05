using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Transactions.Commands;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Domain.Interfaces;

namespace TransactionAggregator.Tests.Unit.Application;

public sealed class IngestTransactionsCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICategorizationService> _categorization = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly Mock<ITransactionDataSource> _dataSource = new();
    private readonly Mock<ITransactionRepository> _transactions = new();

    public IngestTransactionsCommandHandlerTests()
    {
        _unitOfWork.Setup(u => u.Transactions).Returns(_transactions.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _categorization
            .Setup(c => c.CategorizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        _transactions
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dataSource.Setup(s => s.SourceName).Returns("TestSource");
        _dataSource.Setup(s => s.SourceType).Returns(TransactionSource.RestApi);
    }

    private IngestTransactionsCommandHandler CreateHandler(
        IEnumerable<ITransactionDataSource>? sources = null) =>
        new(
            sources ?? [_dataSource.Object],
            _unitOfWork.Object,
            _categorization.Object,
            _cacheService.Object,
            NullLogger<IngestTransactionsCommandHandler>.Instance);

    private static RawTransaction Raw(string externalId, string merchant = "Checkers", decimal amount = 100m) =>
        new()
        {
            ExternalId = externalId,
            Amount = amount,
            Currency = "ZAR",
            MerchantName = merchant,
            TransactionDate = DateTime.UtcNow
        };

    [Fact]
    public async Task Handle_WhenSourceReturnsTransactions_ShouldIngestAll()
    {
        var raw = new List<RawTransaction> { Raw("EXT-001"), Raw("EXT-002", "Uber", 50m) };

        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.TotalReceived.Should().Be(2);
        result.Ingested.Should().Be(2);
        result.Duplicates.Should().Be(0);
        result.Failed.Should().Be(0);
        _transactions.Verify(
            r => r.AddRangeAsync(It.Is<IEnumerable<Transaction>>(t => t.Count() == 2), It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheService.Verify(
            c => c.RemoveByPrefixAsync(CacheKeys.AggregationsPrefix, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTransactionAlreadyExists_ShouldCountAsDuplicate()
    {
        var raw = new List<RawTransaction> { Raw("DUPE-001", "KFC"), Raw("NEW-001") };

        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["DUPE-001"]);

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.TotalReceived.Should().Be(2);
        result.Ingested.Should().Be(1);
        result.Duplicates.Should().Be(1);
        _transactions.Verify(
            r => r.AddRangeAsync(It.Is<IEnumerable<Transaction>>(t => t.Count() == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBatchContainsDuplicateExternalIds_ShouldIngestOnlyFirst()
    {
        var raw = new List<RawTransaction> { Raw("SAME-001"), Raw("SAME-001", "Uber", 50m) };

        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.Ingested.Should().Be(1);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSourceFails_ShouldReturnErrorsAndContinue()
    {
        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.TotalReceived.Should().Be(0);
        result.Ingested.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.Contains("Connection refused"));
        _cacheService.Verify(
            c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenInvalidRawTransaction_ShouldCountAsFailed()
    {
        var raw = new List<RawTransaction>
        {
            new()
            {
                ExternalId = " ",
                Amount = 10m,
                Currency = "ZAR",
                MerchantName = "Checkers",
                TransactionDate = DateTime.UtcNow
            }
        };

        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.Failed.Should().Be(1);
        result.Ingested.Should().Be(0);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCategoryMatched_ShouldCategorizeTransaction()
    {
        var categoryId = Guid.NewGuid();
        var raw = new List<RawTransaction> { Raw("EXT-CAT", "Checkers Hyper") };

        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(raw);
        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _categorization
            .Setup(c => c.CategorizeAsync("Checkers Hyper", It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoryId);

        IReadOnlyList<Transaction>? saved = null;
        _transactions
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Transaction>, CancellationToken>((items, _) => saved = items.ToList())
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.Ingested.Should().Be(1);
        saved.Should().NotBeNull();
        saved![0].CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task Handle_WithSourceOverride_ShouldOnlyUseMatchingSource()
    {
        var matching = new Mock<ITransactionDataSource>();
        matching.Setup(s => s.SourceName).Returns("CsvFileImport");
        matching.Setup(s => s.SourceType).Returns(TransactionSource.FileImport);
        matching.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Raw("CSV-1")]);

        var other = new Mock<ITransactionDataSource>();
        other.Setup(s => s.SourceName).Returns("RestApiMock");
        other.Setup(s => s.SourceType).Returns(TransactionSource.RestApi);

        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler([matching.Object, other.Object])
            .Handle(new IngestTransactionsCommand("CsvFileImport"), default);

        result.TotalReceived.Should().Be(1);
        result.Ingested.Should().Be(1);
        other.Verify(s => s.FetchAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNothingIngested_ShouldNotInvalidateCache()
    {
        _dataSource.Setup(s => s.FetchAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _transactions
            .Setup(r => r.GetExistingExternalIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new IngestTransactionsCommand(), default);

        result.Ingested.Should().Be(0);
        _cacheService.Verify(
            c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
