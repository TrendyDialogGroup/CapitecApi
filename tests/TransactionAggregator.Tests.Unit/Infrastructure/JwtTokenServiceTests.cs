using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TransactionAggregator.Infrastructure.Services;

namespace TransactionAggregator.Tests.Unit.Infrastructure;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_ShouldIncludeEmailRolesIssuerAndAudience()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            SecretKey = new string('k', 48),
            Issuer = "TransactionAggregator",
            Audience = "TransactionAggregatorClient",
            ExpiryMinutes = 60
        }));

        var userId = Guid.NewGuid();
        var token = service.GenerateToken(userId, "admin@example.com", ["Admin", "User"]);

        var payloadJson = DecodeJwtPayload(token);
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        root.GetProperty("sub").GetString().Should().Be(userId.ToString());
        root.GetProperty("email").GetString().Should().Be("admin@example.com");
        root.GetProperty("iss").GetString().Should().Be("TransactionAggregator");
        root.GetProperty("aud").GetString().Should().Be("TransactionAggregatorClient");

        var roles = root.EnumerateObject()
            .First(p => p.Name.Contains("role", StringComparison.OrdinalIgnoreCase))
            .Value;

        var roleValues = roles.ValueKind == JsonValueKind.Array
            ? roles.EnumerateArray().Select(x => x.GetString()).ToArray()
            : [roles.GetString()];

        roleValues.Should().BeEquivalentTo("Admin", "User");
    }

    private static string DecodeJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        parts.Length.Should().Be(3);

        var payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');

        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
    }
}
