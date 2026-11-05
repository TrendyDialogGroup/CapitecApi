using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using TransactionAggregator.Infrastructure.Ingestion;

namespace TransactionAggregator.Tests.Unit.Infrastructure;

public sealed class CsvFileTransactionSourceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "capitec-csv-tests-" + Guid.NewGuid().ToString("N"));

    public CsvFileTransactionSourceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static ResiliencePipelineProvider<string> CreatePipelineProvider()
    {
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("ExternalSource", (builder, _) =>
        {
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero
            });
        });
        return registry;
    }

    private CsvFileTransactionSource CreateSource(string directory) =>
        new(
            Options.Create(new FileSourceOptions { ImportDirectory = directory }),
            CreatePipelineProvider(),
            NullLogger<CsvFileTransactionSource>.Instance);

    [Fact]
    public async Task FetchAsync_WhenDirectoryMissing_ShouldReturnMockData()
    {
        var source = CreateSource(Path.Combine(_tempDir, "missing"));

        var result = await source.FetchAsync();

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(r => r.ExternalId.StartsWith("CSV-"));
        source.SourceName.Should().Be("CsvFileImport");
    }

    [Fact]
    public async Task FetchAsync_WhenCsvPresent_ShouldParseRecords()
    {
        var csvPath = Path.Combine(_tempDir, "sample.csv");
        await File.WriteAllTextAsync(csvPath,
            """
            Reference,Amount,Currency,Merchant,Date
            REF-100,199.99,ZAR,Checkers,2026-03-01T10:00:00Z
            REF-200,55.50,ZAR,Uber,2026-03-02T11:00:00Z
            """);

        var source = CreateSource(_tempDir);
        var result = await source.FetchAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(r => r.ExternalId == "CSV-REF-100" && r.MerchantName == "Checkers" && r.Amount == 199.99m);
        result.Should().Contain(r => r.ExternalId == "CSV-REF-200" && r.MerchantName == "Uber");
    }
}
