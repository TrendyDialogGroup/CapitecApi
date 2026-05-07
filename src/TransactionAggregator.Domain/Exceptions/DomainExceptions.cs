using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Domain.Exceptions;

public sealed class TransactionNotFoundException(Guid id)
    : Exception($"Transaction with id '{id}' was not found.");

public sealed class DuplicateTransactionException(string externalId)
    : Exception($"Transaction with external id '{externalId}' already exists.");

public sealed class CategoryNotFoundException(Guid id)
    : Exception($"Category with id '{id}' was not found.");

public sealed class ValidationException(IEnumerable<string> errors)
    : Exception($"Validation failed: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors.ToList().AsReadOnly();
}
