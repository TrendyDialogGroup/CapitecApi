using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Domain.Interfaces;

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
        var cacheKey = CacheKeys.TotalSpendByCategory;

        return await cache.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                var query = dbContext.Transactions
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
                var transactions = await dbContext.Transactions
                    .Where(t =>
                        t.TransactionDate.Year == request.Year &&
                        t.TransactionDate.Month == request.Month)
                    .Include(t => t.Category)
                    .ToListAsync(cancellationToken);

                var byCategory = transactions
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
                    .ToList();

                return new MonthlySummaryDto
                {
                    Year = request.Year,
                    Month = request.Month,
                    MonthName = new DateTime(request.Year, request.Month, 1).ToString("MMMM"),
                    TotalSpend = transactions.Sum(t => t.Amount),
                    TransactionCount = transactions.Count,
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
        return await cache.GetOrSetAsync(
            CacheKeys.TopMerchants,
            async () => (IReadOnlyList<TopMerchantDto>)await dbContext.Transactions
                .GroupBy(t => new { t.MerchantName, CategoryName = t.Category != null ? t.Category.Name : null })
                .Select(g => new TopMerchantDto
                {
                    MerchantName = g.Key.MerchantName,
                    TotalAmount = g.Sum(t => t.Amount),
                    TransactionCount = g.Count(),
                    CategoryName = g.Key.CategoryName
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(request.Take)
                .ToListAsync(cancellationToken),
            expiry: TimeSpan.FromMinutes(20),
            cancellationToken: cancellationToken);
    }
}
