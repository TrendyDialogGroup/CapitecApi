using Microsoft.AspNetCore.Mvc;
using TransactionAggregator.Application.Common.Interfaces;
using TransactionAggregator.Application.Common.Models;

namespace TransactionAggregator.Api.Controllers;

/// <summary>
/// Lightweight auth endpoint for obtaining JWT tokens.
/// In production, integrate with an identity provider (Entra ID, Auth0, etc.)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class AuthController(IJwtTokenService jwtTokenService) : ControllerBase
{
    /// <summary>
    /// Issues a JWT token for the provided credentials.
    /// Demo endpoint — replace with real identity verification in production.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetToken([FromBody] TokenRequest request)
    {
        // Demo credential check — swap with DB lookup + password hash verification
        if (request.Email == "admin@example.com" && request.Password == "Admin@123!")
        {
            var token = jwtTokenService.GenerateToken(
                Guid.NewGuid(), request.Email, ["Admin", "User"]);

            return Ok(ApiResponse<TokenResponse>.Ok(
                new TokenResponse(token, "Bearer", 3600)));
        }

        if (request.Email == "user@example.com" && request.Password == "User@123!")
        {
            var token = jwtTokenService.GenerateToken(
                Guid.NewGuid(), request.Email, ["User"]);

            return Ok(ApiResponse<TokenResponse>.Ok(
                new TokenResponse(token, "Bearer", 3600)));
        }

        return Unauthorized(ApiResponse<object>.Fail("Invalid credentials."));
    }
}

public sealed record TokenRequest(string Email, string Password);
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
