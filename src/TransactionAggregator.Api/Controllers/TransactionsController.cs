using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Application.Transactions.Commands;
using TransactionAggregator.Application.Transactions.Queries;
using TransactionAggregator.Domain.Enums;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransactionAggregator.Application.Common.Interfaces;

namespace TransactionAggregator.Api.Controllers;

/// <summary>Transaction management endpoints.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "UserOrAdmin")]
[Produces("application/json")]
public sealed class TransactionsController(
    IMediator mediator,
    IBackgroundTaskQueue taskQueue) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated, filtered list of transactions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minAmount,
        [FromQuery] decimal? maxAmount,
        [FromQuery] TransactionSource? source,
        [FromQuery] string? merchantName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetTransactionsQuery(
            dateFrom, dateTo, categoryId, minAmount, maxAmount, source, merchantName, page, pageSize), ct);

        return Ok(ApiResponse<PagedResult<TransactionDto>>.Ok(result));
    }

    /// <summary>
    /// Queues transaction ingestion from all configured data sources in the background.
    /// </summary>
    [HttpPost("ingest")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TriggerIngestion(
        [FromQuery] string? source = null,
        CancellationToken ct = default)
    {
        await taskQueue.QueueBackgroundWorkItemAsync(async (serviceProvider, token) =>
        {
            var scopedMediator = serviceProvider.GetRequiredService<IMediator>();
            var logger = serviceProvider.GetRequiredService<ILogger<TransactionsController>>();
            
            try
            {
                var result = await scopedMediator.Send(new IngestTransactionsCommand(source), token);
                logger.LogInformation("Background ingestion completed successfully. Ingested: {Ingested}", result.Ingested);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background ingestion failed.");
            }
        });

        return Accepted(ApiResponse<string>.Ok("Ingestion queued.", "Ingestion has been queued for background processing."));
    }
}
