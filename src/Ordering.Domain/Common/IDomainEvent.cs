namespace Ordering.Domain.Common;

/// <summary>
/// Something that happened in the domain that other parts of the system may want to react to.
/// Domain events are recorded by aggregates and turned into outbox messages when the aggregate is saved
/// (see Ordering.Infrastructure/Outbox), so they are published reliably and only if the transaction commits.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
