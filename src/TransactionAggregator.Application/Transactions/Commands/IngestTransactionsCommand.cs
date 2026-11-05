using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Domain.Entities;
using TransactionAggregator.Domain.Interfaces;

namespace TransactionAggregator.Application.Transactions.Commands;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record IngestTransactionsCommand(
    string? SourceOverride = null) : IRequest<IngestResultDto>;

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class IngestTransactionsCommandValidator : AbstractValidator<IngestTransactionsCommand>
{
    public IngestTransactionsCommandValidator()
    {
        // No required fields — source override is optional
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class IngestTransactionsCommandHandler(
    IEnumerable<ITransactionDataSource> dataSources,
    IUnitOfWork unitOfWork,
    ICategorizationService categorizationService,
    ICacheService cacheService,
    ILogger<IngestTransactionsCommandHandler> logger)
    : IRequestHandler<IngestTransactionsCommand, IngestResultDto>
{
    public async Task<IngestResultDto> Handle(
        IngestTransactionsCommand request,
        CancellationToken cancellationToken)
    {
        var sources = string.IsNullOrEmpty(request.SourceOverride)
            ? dataSources
            : dataSources.Where(s => s.SourceName.Equals(request.SourceOverride, StringComparison.OrdinalIgnoreCase));

        int totalReceived = 0, ingested = 0, duplicates = 0, failed = 0;
        var errors = new List<string>();

        foreach (var source in sources)
        {
            logger.LogInformation("Fetching transactions from source: {SourceName}", source.SourceName);

            IReadOnlyList<RawTransaction> raw;
            try
            {
                raw = await source.FetchAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch from source {SourceName}", source.SourceName);
                errors.Add($"Source '{source.SourceName}': {ex.Message}");
                continue;
            }

            totalReceived += raw.Count;
            logger.LogInformation("Received {Count} transactions from {SourceName}", raw.Count, source.SourceName);

            // Fetch all existing IDs in one go (batch check) to avoid N+1 problem
            var rawIds = raw.Select(r => r.ExternalId).ToList();
            var existingIds = new HashSet<string>(await unitOfWork.Transactions.GetExistingExternalIdsAsync(rawIds, cancellationToken));

            var transactionsToInsert = new List<Transaction>();

            foreach (var rawTx in raw)
            {
                try
                {
                    // Idempotency check (hashset O(1) lookup instead of DB query)
                    if (existingIds.Contains(rawTx.ExternalId))
                    {
                        duplicates++;
                        continue;
                    }

                    var transaction = Transaction.Create(
                        rawTx.ExternalId,
                        rawTx.Amount,
                        rawTx.Currency,
                        rawTx.MerchantName,
                        rawTx.TransactionDate,
                        source.SourceType);

                    // Auto-categorize
                    var categoryId = await categorizationService.CategorizeAsync(rawTx.MerchantName, cancellationToken);
                    if (categoryId.HasValue)
                        transaction.Categorize(categoryId.Value);

                    transactionsToInsert.Add(transaction);
                    existingIds.Add(rawTx.ExternalId); // Prevent dupes within the same batch
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process transaction {ExternalId}", rawTx.ExternalId);
                    errors.Add($"Transaction '{rawTx.ExternalId}': {ex.Message}");
                    failed++;
                }
            }

            if (transactionsToInsert.Count > 0)
            {
                await unitOfWork.Transactions.AddRangeAsync(transactionsToInsert, cancellationToken);
                ingested += transactionsToInsert.Count;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate aggregations cache if any transactions were ingested
        if (ingested > 0)
        {
            logger.LogInformation("Transactions ingested, invalidating aggregation caches...");
            await cacheService.RemoveByPrefixAsync(CacheKeys.AggregationsPrefix, cancellationToken);
        }

        logger.LogInformation(
            "Ingestion complete — Received: {Total}, Ingested: {Ingested}, Duplicates: {Dupes}, Failed: {Failed}",
            totalReceived, ingested, duplicates, failed);

        return new IngestResultDto
        {
            TotalReceived = totalReceived,
            Ingested = ingested,
            Duplicates = duplicates,
            Failed = failed,
            Errors = errors.AsReadOnly()
        };
    }
}
