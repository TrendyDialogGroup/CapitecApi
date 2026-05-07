using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregator.Application.Aggregations.Queries;
using TransactionAggregator.Application.Common.Models;

namespace TransactionAggregator.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "UserOrAdmin")]
[Produces("application/json")]
public sealed class AggregationsController(IMediator mediator) : ControllerBase
{

    [HttpGet("total-spend-by-category")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SpendByCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotalSpendByCategory(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTotalSpendByCategoryQuery(dateFrom, dateTo), ct);
        return Ok(ApiResponse<IReadOnlyList<SpendByCategoryDto>>.Ok(result));
    }

    [HttpGet("monthly-summary")]
    [ProducesResponseType(typeof(ApiResponse<MonthlySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        if (year < 2000 || year > DateTime.UtcNow.Year + 1)
            return BadRequest(ApiResponse<object>.Fail("Invalid year."));
        if (month < 1 || month > 12)
            return BadRequest(ApiResponse<object>.Fail("Month must be between 1 and 12."));

        var result = await mediator.Send(new GetMonthlySummaryQuery(year, month), ct);
        return Ok(ApiResponse<MonthlySummaryDto>.Ok(result));
    }

    [HttpGet("top-merchants")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TopMerchantDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopMerchants(
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        if (take is < 1 or > 50)
            return BadRequest(ApiResponse<object>.Fail("Take must be between 1 and 50."));

        var result = await mediator.Send(new GetTopMerchantsQuery(take), ct);
        return Ok(ApiResponse<IReadOnlyList<TopMerchantDto>>.Ok(result));
    }
}
