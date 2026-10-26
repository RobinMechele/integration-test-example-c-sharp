namespace Ordering.Infrastructure.Messaging;

/// <summary>
/// Port to the message broker (RabbitMQ, Azure Service Bus, Kafka, ...). The broker is an
/// <i>unmanaged</i> dependency: other systems consume what we publish, so the messages are part of our
/// observable behaviour. Tests therefore replace this interface with a spy and assert on what was published.
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}

/// <param name="MessageId">Stable id; consumers use it to de-duplicate (the outbox guarantees at-least-once delivery).</param>
/// <param name="MessageType">The event name, e.g. "OrderPlaced".</param>
/// <param name="Payload">The event serialised as JSON.</param>
/// <param name="OccurredOnUtc">When the event happened in the domain.</param>
public sealed record OutgoingMessage(Guid MessageId, string MessageType, string Payload, DateTimeOffset OccurredOnUtc);
