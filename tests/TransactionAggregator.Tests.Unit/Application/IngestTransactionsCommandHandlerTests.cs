using AutoMapper;
using FluentAssertions;
using Moq;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Common.Mappings;
using TransactionAggregator.Application.Transactions.Commands;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace TransactionAggregator.Tests.Unit.Application;

public sealed class IngestTransactionsCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICategorizationService> _categorization = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly Mock<ITransactionDataSource> _dataSource1 = new();
    private readonly IMapper _mapper;

    public IngestTransactionsCommandHandlerTests()
    {
        _mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
    }

    private IngestTransactionsCommandHandler CreateHandler()
    {
        var transactions = new Mock<ITransactionRepository>();
        transactions.Setup(r => r.ExistsByExternalIdAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), default))
            .Returns(Task.CompletedTask);

        _unitOfWork.Setup(u => u.Transactions).Returns(transactions.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        _categorization.Setup(c => c.CategorizeAsync(It.IsAny<string>(), default))
            .ReturnsAsync((Guid?)null);

        return new IngestTransactionsCommandHandler(
            [_dataSource1.Object],
            _unitOfWork.Object,
            _categorization.Object,
            _cacheService.Object,
            _mapper,
            NullLogger<IngestTransactionsCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenSourceReturnsTransactions_ShouldIngestAll()
    {
        // Arrange
        var raw = new List<RawTransaction>
        {
            new() { ExternalId = "EXT-001", Amount = 100, Currency = "ZAR", MerchantName = "Checkers", TransactionDate = DateTime.UtcNow },
            new() { ExternalId = "EXT-002", Amount = 50,  Currency = "ZAR", MerchantName = "Uber",     TransactionDate = DateTime.UtcNow },
        };

        _dataSource1.Setup(s => s.SourceName).Returns("TestSource");
        _dataSource1.Setup(s => s.SourceType).Returns(TransactionSource.RestApi);
        _dataSource1.Setup(s => s.FetchAsync(default)).ReturnsAsync(raw);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new IngestTransactionsCommand(), default);

        // Assert
        result.TotalReceived.Should().Be(2);
        result.Ingested.Should().Be(2);
        result.Duplicates.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTransactionAlreadyExists_ShouldCountAsDuplicate()
    {
        // Arrange
        var txRepo = new Mock<ITransactionRepository>();
        txRepo.Setup(r => r.ExistsByExternalIdAsync("DUPE-001", default)).ReturnsAsync(true);
        txRepo.Setup(r => r.ExistsByExternalIdAsync("NEW-001", default)).ReturnsAsync(false);
        txRepo.Setup(r => r.AddAsync(It.IsAny<Transaction>(), default)).Returns(Task.CompletedTask);

        _unitOfWork.Setup(u => u.Transactions).Returns(txRepo.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var raw = new List<RawTransaction>
        {
            new() { ExternalId = "DUPE-001", Amount = 100, Currency = "ZAR", MerchantName = "KFC",    TransactionDate = DateTime.UtcNow },
            new() { ExternalId = "NEW-001",  Amount = 200, Currency = "ZAR", MerchantName = "Checkers",TransactionDate = DateTime.UtcNow },
        };

        _dataSource1.Setup(s => s.SourceName).Returns("TestSource");
        _dataSource1.Setup(s => s.SourceType).Returns(TransactionSource.RestApi);
        _dataSource1.Setup(s => s.FetchAsync(default)).ReturnsAsync(raw);

        var handler = new IngestTransactionsCommandHandler(
            [_dataSource1.Object],
            _unitOfWork.Object,
            _categorization.Object,
            _cacheService.Object,
            _mapper,
            NullLogger<IngestTransactionsCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new IngestTransactionsCommand(), default);

        // Assert
        result.TotalReceived.Should().Be(2);
        result.Ingested.Should().Be(1);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSourceFails_ShouldReturnErrors()
    {
        // Arrange
        _dataSource1.Setup(s => s.SourceName).Returns("BrokenSource");
        _dataSource1.Setup(s => s.SourceType).Returns(TransactionSource.RestApi);
        _dataSource1.Setup(s => s.FetchAsync(default))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(0);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new IngestTransactionsCommand(), default);

        // Assert
        result.TotalReceived.Should().Be(0);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Connection refused");
    }
}
