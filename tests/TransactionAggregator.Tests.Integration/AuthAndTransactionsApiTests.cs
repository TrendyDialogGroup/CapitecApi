using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Tests.Integration.Infrastructure;

namespace TransactionAggregator.Tests.Integration;

public sealed class AuthAndTransactionsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;

    public AuthAndTransactionsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/Auth/token", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>(JsonOptions);
        payload!.Success.Should().BeTrue();
        payload.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        return payload.Data.AccessToken;
    }

    [Fact]
    public async Task Auth_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/Auth/token", new
        {
            email = "nope@example.com",
            password = "wrong"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTransactions_WithoutToken_ShouldReturnUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/Transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTransactions_WithUserToken_ShouldReturnEmptyPage()
    {
        var client = CreateClient();
        var token = await GetTokenAsync(client, "user@example.com", "User@123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/Transactions?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TransactionDto>>>(JsonOptions);
        payload!.Success.Should().BeTrue();
        payload.Data!.Items.Should().NotBeNull();
        payload.Data.Page.Should().Be(1);
    }

    [Fact]
    public async Task Ingest_WithUserToken_ShouldReturnForbidden()
    {
        var client = CreateClient();
        var token = await GetTokenAsync(client, "user@example.com", "User@123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/v1/Transactions/ingest", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ingest_WithAdminToken_ShouldQueueAndPersistTransactions()
    {
        var client = CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var fakeSource = scope.ServiceProvider.GetRequiredService<FakeTransactionDataSource>();
            fakeSource.SetTransactions(
                new RawTransaction
                {
                    ExternalId = $"INT-{Guid.NewGuid():N}",
                    Amount = 120.50m,
                    Currency = "ZAR",
                    MerchantName = "Checkers",
                    TransactionDate = DateTime.UtcNow
                },
                new RawTransaction
                {
                    ExternalId = $"INT-{Guid.NewGuid():N}",
                    Amount = 45m,
                    Currency = "ZAR",
                    MerchantName = "Uber",
                    TransactionDate = DateTime.UtcNow
                });
        }

        var token = await GetTokenAsync(client, "admin@example.com", "Admin@123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var ingestResponse = await client.PostAsync("/api/v1/Transactions/ingest", null);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Background queue may process asynchronously — poll briefly for visibility.
        ApiResponse<PagedResult<TransactionDto>>? listPayload = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(100);
            var listResponse = await client.GetAsync("/api/v1/Transactions?page=1&pageSize=50");
            listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            listPayload = await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TransactionDto>>>(JsonOptions);
            if (listPayload?.Data?.TotalCount >= 2)
                break;
        }

        listPayload!.Data!.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        listPayload.Data.Items.Should().Contain(i => i.MerchantName == "Checkers");
        listPayload.Data.Items.Should().Contain(i => i.MerchantName == "Uber");
    }

    [Fact]
    public async Task Aggregations_WithUserToken_ShouldReturnOk()
    {
        var client = CreateClient();
        var token = await GetTokenAsync(client, "user@example.com", "User@123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var spend = await client.GetAsync("/api/v1/Aggregations/total-spend-by-category");
        var monthly = await client.GetAsync($"/api/v1/Aggregations/monthly-summary?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}");
        var merchants = await client.GetAsync("/api/v1/Aggregations/top-merchants?take=5");

        spend.StatusCode.Should().Be(HttpStatusCode.OK);
        monthly.StatusCode.Should().Be(HttpStatusCode.OK);
        merchants.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ShouldReturnHealthyDatabase()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Healthy");
        body.Should().Contain("database");
    }

    private sealed record TokenDto(string AccessToken, string TokenType, int ExpiresIn);
}
