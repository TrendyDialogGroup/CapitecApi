using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TransactionAggregator.Application.Common.Mappings;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Application.Transactions.Queries;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Domain.Interfaces;

namespace TransactionAggregator.Tests.Unit.Application;

public sealed class GetTransactionsQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly IMapper _mapper;

    public GetTransactionsQueryHandlerTests()
    {
        _mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

        _unitOfWork.Setup(u => u.Transactions).Returns(_transactions.Object);
    }

    [Fact]
    public async Task Handle_ShouldMapPagedResults()
    {
        var tx = Transaction.Create("EXT-1", 120m, "ZAR", "Checkers", DateTime.UtcNow, TransactionSource.RestApi);

        _transactions
            .Setup(r => r.GetPagedAsync(
                It.IsAny<TransactionFilter>(),
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Transaction> { tx }.AsReadOnly(), 1));

        var handler = new GetTransactionsQueryHandler(_unitOfWork.Object, _mapper);

        var result = await handler.Handle(new GetTransactionsQuery(
            null, null, null, null, null, null, null, 1, 20), default);

        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].ExternalId.Should().Be("EXT-1");
        result.Items[0].MerchantName.Should().Be("Checkers");
        result.Items[0].Source.Should().Be(nameof(TransactionSource.RestApi));
    }

    [Fact]
    public async Task Handle_ShouldPassFiltersToRepository()
    {
        TransactionFilter? captured = null;
        _transactions
            .Setup(r => r.GetPagedAsync(
                It.IsAny<TransactionFilter>(),
                2,
                10,
                It.IsAny<CancellationToken>()))
            .Callback<TransactionFilter, int, int, CancellationToken>((filter, _, _, _) => captured = filter)
            .ReturnsAsync((Array.Empty<Transaction>(), 0));

        var categoryId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var handler = new GetTransactionsQueryHandler(_unitOfWork.Object, _mapper);
        await handler.Handle(new GetTransactionsQuery(
            from, to, categoryId, 10m, 500m, TransactionSource.FileImport, "Uber", 2, 10), default);

        captured.Should().NotBeNull();
        captured!.DateFrom.Should().Be(from);
        captured.DateTo.Should().Be(to);
        captured.CategoryId.Should().Be(categoryId);
        captured.MinAmount.Should().Be(10m);
        captured.MaxAmount.Should().Be(500m);
        captured.Source.Should().Be(TransactionSource.FileImport);
        captured.MerchantName.Should().Be("Uber");
    }
}

public sealed class GetTransactionsQueryValidatorTests
{
    private readonly GetTransactionsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenPageInvalid_ShouldFail()
    {
        var result = _validator.Validate(new GetTransactionsQuery(
            null, null, null, null, null, null, null, Page: 0, PageSize: 20));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetTransactionsQuery.Page));
    }

    [Fact]
    public void Validate_WhenDateRangeInvalid_ShouldFail()
    {
        var result = _validator.Validate(new GetTransactionsQuery(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-1),
            null, null, null, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenValid_ShouldPass()
    {
        var result = _validator.Validate(new GetTransactionsQuery(
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            null, 1m, 100m, null, null, 1, 50));

        result.IsValid.Should().BeTrue();
    }
}
