namespace TransactionAggregator.Domain.Common;

/// <summary>
/// Base entity providing identity, audit fields and domain event support.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void SetUpdatedAt() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent;
