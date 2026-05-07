using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace TransactionAggregator.Api.Middleware;

/// <summary>
/// Extracts or generates a Correlation ID for the request and pushes it into the Serilog LogContext.
/// Also attaches the Correlation ID to the HTTP response headers.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);
        var userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown"
            : "Anonymous";

        // Add to response header
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        // Push to Serilog's LogContext for structured logging and auditing
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("ClientIp", context.Connection.RemoteIpAddress?.ToString() ?? "Unknown"))
        {
            await next(context);
        }
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out StringValues headerValues))
        {
            var value = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return Guid.NewGuid().ToString("N");
    }
}
