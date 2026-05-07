using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Domain.Exceptions;

namespace TransactionAggregator.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Converts all unhandled exceptions into a standardised error response.
/// Prevents leaking stack traces or internal details to clients.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message, errors) = ex switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "Validation failed.",
                (IReadOnlyList<string>?)ve.Errors),

            TransactionNotFoundException or CategoryNotFoundException => (
                HttpStatusCode.NotFound,
                ex.Message,
                (IReadOnlyList<string>?)null),

            DuplicateTransactionException => (
                HttpStatusCode.Conflict,
                ex.Message,
                (IReadOnlyList<string>?)null),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized.",
                (IReadOnlyList<string>?)null),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                (IReadOnlyList<string>?)null)
        };

        logger.LogError(ex, "Unhandled exception — {StatusCode}: {Message}", (int)statusCode, ex.Message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Fail(message, errors);
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions));
    }
}
