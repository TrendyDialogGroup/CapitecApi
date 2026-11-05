using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregator.Application.Common.Behaviours;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Application.Transactions.Queries;
using DomainValidationException = TransactionAggregator.Domain.Exceptions.ValidationException;

namespace TransactionAggregator.Tests.Unit.Application;

public sealed class ValidationBehaviourTests
{
    [Fact]
    public async Task Handle_WhenValidationFails_ShouldThrowDomainValidationException()
    {
        var behaviour = new ValidationBehaviour<GetTransactionsQuery, PagedResult<TransactionDto>>(
            [new GetTransactionsQueryValidator()],
            NullLogger<ValidationBehaviour<GetTransactionsQuery, PagedResult<TransactionDto>>>.Instance);

        var request = new GetTransactionsQuery(null, null, null, null, null, null, null, Page: 0, PageSize: 20);

        var act = () => behaviour.Handle(
            request,
            () => Task.FromResult(new PagedResult<TransactionDto>()),
            default);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_ShouldCallNext()
    {
        var behaviour = new ValidationBehaviour<GetTransactionsQuery, string>(
            [new GetTransactionsQueryValidator()],
            NullLogger<ValidationBehaviour<GetTransactionsQuery, string>>.Instance);

        var request = new GetTransactionsQuery(null, null, null, null, null, null, null, 1, 20);
        var called = false;

        var result = await behaviour.Handle(request, () =>
        {
            called = true;
            return Task.FromResult("ok");
        }, default);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        var behaviour = new ValidationBehaviour<GetTransactionsQuery, string>(
            Array.Empty<IValidator<GetTransactionsQuery>>(),
            NullLogger<ValidationBehaviour<GetTransactionsQuery, string>>.Instance);

        var result = await behaviour.Handle(
            new GetTransactionsQuery(null, null, null, null, null, null, null, 0, 0),
            () => Task.FromResult("skipped"),
            default);

        result.Should().Be("skipped");
    }
}
