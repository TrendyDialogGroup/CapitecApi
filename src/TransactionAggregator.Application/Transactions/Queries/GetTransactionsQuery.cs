using AutoMapper;
using FluentValidation;
using MediatR;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Domain.Enums;
using TransactionAggregator.Domain.Interfaces;

namespace TransactionAggregator.Application.Transactions.Queries;

// ── Query ────────────────────────────────────────────────────────────────────

public sealed record GetTransactionsQuery(
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? CategoryId,
    decimal? MinAmount,
    decimal? MaxAmount,
    TransactionSource? Source,
    string? MerchantName,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<TransactionDto>>;

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class GetTransactionsQueryValidator : AbstractValidator<GetTransactionsQuery>
{
    public GetTransactionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        When(x => x.DateFrom.HasValue && x.DateTo.HasValue, () =>
        {
            RuleFor(x => x.DateTo)
                .GreaterThanOrEqualTo(x => x.DateFrom)
                .WithMessage("DateTo must be greater than or equal to DateFrom.");
        });

        When(x => x.MinAmount.HasValue && x.MaxAmount.HasValue, () =>
        {
            RuleFor(x => x.MaxAmount)
                .GreaterThanOrEqualTo(x => x.MinAmount)
                .WithMessage("MaxAmount must be greater than or equal to MinAmount.");
        });
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class GetTransactionsQueryHandler(
    IUnitOfWork unitOfWork,
    IMapper mapper)
    : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionDto>>
{
    public async Task<PagedResult<TransactionDto>> Handle(
        GetTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var filter = new TransactionFilter
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            CategoryId = request.CategoryId,
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            Source = request.Source,
            MerchantName = request.MerchantName
        };

        var (items, totalCount) = await unitOfWork.Transactions.GetPagedAsync(
            filter, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<TransactionDto>
        {
            Items = mapper.Map<IReadOnlyList<TransactionDto>>(items),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
