using System.Text.Json;
using Ordering.Domain.Common;

namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// A row in OUTBOX_MESSAGES: a domain event waiting to be published to the message broker.
/// Written in the same transaction as the business data (see <see cref="InsertOutboxMessagesInterceptor"/>),
/// published later by <see cref="OutboxProcessor"/>.
/// </summary>
public sealed class OutboxMessage
{
    public const int MessageTypeMaxLength = 200;
    public const int LastErrorMaxLength = 2000;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; init; }

    public required string MessageType { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset OccurredOnUtc { get; init; }

    public DateTimeOffset? ProcessedOnUtc { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public static OutboxMessage FromDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var eventType = domainEvent.GetType();

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            // The type name is part of the contract with consumers, so keep event names stable.
            MessageType = eventType.Name,
            Payload = JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
            OccurredOnUtc = domainEvent.OccurredOnUtc,
        };
    }
}
