using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TransactionAggregator.Application.Common.Behaviours;

public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning("Validation failed for {RequestType}: {@Errors}",
                typeof(TRequest).Name, failures.Select(f => f.ErrorMessage));

            throw new Domain.Exceptions.ValidationException(failures.Select(f => f.ErrorMessage));
        }

        return await next();
    }
}
