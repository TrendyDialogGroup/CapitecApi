using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TransactionAggregator.Application.Common.Interfaces;

namespace TransactionAggregator.Infrastructure.Services;

/// <summary>
/// JWT token generation — produces signed access tokens.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateToken(Guid userId, string email, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SecretKey { get; init; } = null!;
    public string Issuer { get; init; } = "TransactionAggregator";
    public string Audience { get; init; } = "TransactionAggregatorClient";
    public int ExpiryMinutes { get; init; } = 60;
}
