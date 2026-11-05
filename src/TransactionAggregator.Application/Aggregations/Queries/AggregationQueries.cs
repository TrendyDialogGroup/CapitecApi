using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Common.Models;

namespace TransactionAggregator.Application.Aggregations.Queries;

// ── Total Spend By Category ───────────────────────────────────────────────────

public sealed record GetTotalSpendByCategoryQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo = null) : IRequest<IReadOnlyList<SpendByCategoryDto>>;

public sealed class GetTotalSpendByCategoryQueryHandler(
    IApplicationDbContext dbContext,
    ICacheService cache)
    : IRequestHandler<GetTotalSpendByCategoryQuery, IReadOnlyList<SpendByCategoryDto>>
{
    public async Task<IReadOnlyList<SpendByCategoryDto>> Handle(
        GetTotalSpendByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.TotalSpendByCategory(request.DateFrom, request.DateTo);

        return await cache.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                var query = dbContext.Transactions
                    .AsNoTracking()
                    .Where(t => t.CategoryId != null);

                if (request.DateFrom.HasValue)
                    query = query.Where(t => t.TransactionDate >= request.DateFrom.Value);
                if (request.DateTo.HasValue)
                    query = query.Where(t => t.TransactionDate <= request.DateTo.Value);

                return (IReadOnlyList<SpendByCategoryDto>)await query
                    .GroupBy(t => new { t.CategoryId, t.Category!.Name, t.Currency })
                    .Select(g => new SpendByCategoryDto
                    {
                        CategoryId = g.Key.CategoryId!.Value,
                        CategoryName = g.Key.Name,
                        TotalAmount = g.Sum(t => t.Amount),
                        TransactionCount = g.Count(),
                        Currency = g.Key.Currency
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToListAsync(cancellationToken);
            },
            expiry: TimeSpan.FromMinutes(15),
            cancellationToken: cancellationToken);
    }
}

// ── Monthly Summary ───────────────────────────────────────────────────────────

public sealed record GetMonthlySummaryQuery(int Year, int Month) : IRequest<MonthlySummaryDto>;

public sealed class GetMonthlySummaryQueryHandler(
    IApplicationDbContext dbContext,
    ICacheService cache)
    : IRequestHandler<GetMonthlySummaryQuery, MonthlySummaryDto>
{
    public async Task<MonthlySummaryDto> Handle(
        GetMonthlySummaryQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.MonthlySummary(request.Year, request.Month);

        return await cache.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                // Prefer an inclusive date range over Year/Month projections so SQL can use indexes.
                var periodStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var periodEnd = periodStart.AddMonths(1);

                var monthQuery = dbContext.Transactions
                    .AsNoTracking()
                    .Where(t => t.TransactionDate >= periodStart && t.TransactionDate < periodEnd);

                var totalSpend = await monthQuery.SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
                var transactionCount = await monthQuery.CountAsync(cancellationToken);

                var byCategory = await monthQuery
                    .Where(t => t.CategoryId != null)
                    .GroupBy(t => new { t.CategoryId, t.Category!.Name, t.Currency })
                    .Select(g => new SpendByCategoryDto
                    {
                        CategoryId = g.Key.CategoryId!.Value,
                        CategoryName = g.Key.Name,
                        TotalAmount = g.Sum(t => t.Amount),
                        TransactionCount = g.Count(),
                        Currency = g.Key.Currency
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToListAsync(cancellationToken);

                return new MonthlySummaryDto
                {
                    Year = request.Year,
                    Month = request.Month,
                    MonthName = periodStart.ToString("MMMM"),
                    TotalSpend = totalSpend,
                    TransactionCount = transactionCount,
                    ByCategory = byCategory.AsReadOnly()
                };
            },
            expiry: TimeSpan.FromMinutes(30),
            cancellationToken: cancellationToken);
    }
}

// ── Top Merchants ─────────────────────────────────────────────────────────────

public sealed record GetTopMerchantsQuery(int Take = 10) : IRequest<IReadOnlyList<TopMerchantDto>>;

public sealed class GetTopMerchantsQueryHandler(
    IApplicationDbContext dbContext,
    ICacheService cache)
    : IRequestHandler<GetTopMerchantsQuery, IReadOnlyList<TopMerchantDto>>
{
    public async Task<IReadOnlyList<TopMerchantDto>> Handle(
        GetTopMerchantsQuery request,
        CancellationToken cancellationToken)
    {
        var take = request.Take <= 0 ? 10 : request.Take;

        return await cache.GetOrSetAsync(
            CacheKeys.TopMerchants(take),
            async () => (IReadOnlyList<TopMerchantDto>)await dbContext.Transactions
                .AsNoTracking()
                .GroupBy(t => new { t.MerchantName, CategoryName = t.Category != null ? t.Category.Name : null })
                .Select(g => new TopMerchantDto
                {
                    MerchantName = g.Key.MerchantName,
                    TotalAmount = g.Sum(t => t.Amount),
                    TransactionCount = g.Count(),
                    CategoryName = g.Key.CategoryName
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(take)
                .ToListAsync(cancellationToken),
            expiry: TimeSpan.FromMinutes(20),
            cancellationToken: cancellationToken);
    }
}
