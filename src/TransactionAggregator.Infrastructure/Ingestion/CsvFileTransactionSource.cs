using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Registry;
using System.Globalization;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Domain.Enums;

namespace TransactionAggregator.Infrastructure.Ingestion;

/// <summary>
/// File-based CSV transaction source.
/// Reads from a configured directory path, processes all *.csv files.
/// In a production system this would watch a network share or S3 bucket.
/// Utilizes Polly Resilience Pipeline for automatic retries on file locks.
/// </summary>
public sealed class CsvFileTransactionSource(
    IOptions<FileSourceOptions> options,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<CsvFileTransactionSource> logger) : ITransactionDataSource
{
    public string SourceName => "CsvFileImport";
    public TransactionSource SourceType => TransactionSource.FileImport;

    public async Task<IReadOnlyList<RawTransaction>> FetchAsync(CancellationToken ct = default)
    {
        var dir = options.Value.ImportDirectory;
        var results = new List<RawTransaction>();

        if (!Directory.Exists(dir))
        {
            logger.LogWarning("CSV import directory does not exist: {Dir}. Using mock data.", dir);
            return GenerateMockCsvTransactions();
        }

        var files = Directory.GetFiles(dir, "*.csv");
        logger.LogInformation("Found {Count} CSV files in {Dir}", files.Length, dir);

        var pipeline = pipelineProvider.GetPipeline("ExternalSource");

        foreach (var file in files)
        {
            try
            {
                await pipeline.ExecuteAsync(async token =>
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null,
                        BadDataFound = null,
                    };

                    // Retry protects against transient IOException (e.g. file locked by another process)
                    using var reader = new StreamReader(file);
                    using var csv = new CsvReader(reader, config);

                    var records = csv.GetRecordsAsync<CsvTransactionRecord>(token);
                    await foreach (var record in records.WithCancellation(token))
                    {
                        results.Add(new RawTransaction
                        {
                            ExternalId = $"CSV-{record.Reference ?? Guid.NewGuid().ToString("N")}",
                            Amount = record.Amount,
                            Currency = record.Currency ?? "ZAR",
                            MerchantName = record.Merchant ?? "Unknown",
                            TransactionDate = record.Date
                        });
                    }

                    logger.LogInformation("Parsed {Count} records from {File}", results.Count, file);
                }, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse CSV file: {File}", file);
            }
        }

        return results.Count > 0 ? results : GenerateMockCsvTransactions();
    }

    private static List<RawTransaction> GenerateMockCsvTransactions()
    {
        var rng = new Random(42);
        var merchants = new[] { "Airbnb", "FlySkySafair", "Netcare Hospital", "Mediclinic", "Showmax", "DSTV", "Mr Price", "H&M" };
        var now = DateTime.UtcNow;

        return Enumerable.Range(0, rng.Next(10, 20))
            .Select(_ => new RawTransaction
            {
                ExternalId = $"CSV-{Guid.NewGuid():N}",
                Amount = Math.Round((decimal)(rng.NextDouble() * 2000 + 50), 2),
                Currency = "ZAR",
                MerchantName = merchants[rng.Next(merchants.Length)],
                TransactionDate = now.AddDays(-rng.Next(0, 30))
            })
            .ToList();
    }
}

public sealed class FileSourceOptions
{
    public const string SectionName = "FileSource";
    public string ImportDirectory { get; init; } = "data/imports";
}

/// <summary>CSV record POCO — maps column headers.</summary>
public sealed class CsvTransactionRecord
{
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Merchant { get; set; }
    public DateTime Date { get; set; }
}
