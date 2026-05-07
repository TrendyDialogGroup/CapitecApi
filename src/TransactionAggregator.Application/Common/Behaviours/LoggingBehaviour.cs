using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TransactionAggregator.Application.Common.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("[{RequestName}] Handling request", requestName);

        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                logger.LogWarning(
                    "[{RequestName}] Slow request detected — completed in {ElapsedMs}ms",
                    requestName, sw.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "[{RequestName}] Handled successfully in {ElapsedMs}ms",
                    requestName, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "[{RequestName}] Request failed after {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
