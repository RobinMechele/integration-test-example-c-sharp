using Microsoft.Extensions.Logging;

namespace Ordering.Infrastructure.Messaging;

/// <summary>
/// Stand-in for a real broker client so the sample runs without extra infrastructure.
/// Replace this registration with your broker's publisher (RabbitMQ, Azure Service Bus, Kafka, ...).
/// </summary>
internal sealed partial class LoggingMessagePublisher(ILogger<LoggingMessagePublisher> logger) : IMessagePublisher
{
    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        LogPublished(message.MessageType, message.MessageId, message.Payload);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {MessageType} message {MessageId}: {Payload}")]
    private partial void LogPublished(string messageType, Guid messageId, string payload);
}
