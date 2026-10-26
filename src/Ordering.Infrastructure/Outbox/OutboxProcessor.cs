using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Ordering.Infrastructure.Messaging;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// The "relay" half of the transactional outbox (Chris Richardson's "Polling publisher").
/// Each call claims one batch of unpublished messages, publishes them and marks them as processed,
/// all inside one database transaction.
/// </summary>
/// <remarks>
/// Several processors (several app instances) may run at the same time. The batch is claimed with
/// <c>SELECT ... FOR UPDATE SKIP LOCKED</c>, so each processor gets different rows and a message is never
/// published by two processors at once. Delivery is still at-least-once: if the process crashes after publishing
/// but before committing, the message is published again, so consumers must de-duplicate on the message id.
/// </remarks>
public sealed partial class OutboxProcessor(
    OrderingDbContext dbContext,
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    // Oracle does not allow "FETCH FIRST n ROWS ONLY" together with "FOR UPDATE" (ORA-02014), and a plain
    // "SELECT ... FOR UPDATE SKIP LOCKED" from ADO.NET locks every matching row. The idiomatic Oracle way to claim
    // "the next n unlocked rows" is a PL/SQL cursor with SKIP LOCKED and BULK COLLECT ... LIMIT n: rows are only
    // locked as they are fetched, so concurrent processors receive disjoint batches.
    private const string ClaimBatchSql = """
        DECLARE
          CURSOR pending IS
            SELECT ID
              FROM OUTBOX_MESSAGES
             WHERE PROCESSED_ON_UTC IS NULL
               AND ATTEMPT_COUNT < :max_attempts
             ORDER BY OCCURRED_ON_UTC, ID
               FOR UPDATE SKIP LOCKED;
          claimed SYS.ODCIRAWLIST;
        BEGIN
          OPEN pending;
          FETCH pending BULK COLLECT INTO claimed LIMIT :batch_size;
          CLOSE pending;
          OPEN :messages FOR
            SELECT ID, MESSAGE_TYPE, PAYLOAD, OCCURRED_ON_UTC
              FROM OUTBOX_MESSAGES
             WHERE ID IN (SELECT COLUMN_VALUE FROM TABLE(claimed))
             ORDER BY OCCURRED_ON_UTC, ID;
        END;
        """;

    /// <summary>Processes one batch and returns the number of messages that were claimed.</summary>
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var batch = await ClaimBatchAsync(transaction, settings, cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failing message must not block the others; it is retried on a later run.
                LogPublishFailed(exception, message.MessageType, message.MessageId);
                await RecordFailedAttemptAsync(message.MessageId, exception, cancellationToken);
                continue;
            }

            // If this (or the commit) fails, the whole batch rolls back and is published again later:
            // at-least-once delivery, which is why consumers must de-duplicate on the message id.
            await MarkProcessedAsync(message.MessageId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }

    private Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var processedOnUtc = timeProvider.GetUtcNow();
        return dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(m => m.ProcessedOnUtc, processedOnUtc)
                    .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                    .SetProperty(m => m.LastError, (string?)null),
                cancellationToken);
    }

    private Task RecordFailedAttemptAsync(Guid messageId, Exception exception, CancellationToken cancellationToken)
    {
        var error = exception.ToString();
        if (error.Length > OutboxMessage.LastErrorMaxLength)
        {
            error = error[..OutboxMessage.LastErrorMaxLength];
        }

        return dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                    .SetProperty(m => m.LastError, error),
                cancellationToken);
    }

    private async Task<List<OutgoingMessage>> ClaimBatchAsync(
        IDbContextTransaction transaction,
        OutboxOptions settings,
        CancellationToken cancellationToken)
    {
        var connection = (OracleConnection)dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (OracleTransaction)transaction.GetDbTransaction();
        command.BindByName = true;
        command.CommandText = ClaimBatchSql;
        command.Parameters.Add("max_attempts", OracleDbType.Int32).Value = settings.MaxAttempts;
        command.Parameters.Add("batch_size", OracleDbType.Int32).Value = settings.BatchSize;
        var messagesParameter = command.Parameters.Add("messages", OracleDbType.RefCursor, ParameterDirection.Output);

        await command.ExecuteNonQueryAsync(cancellationToken);

        using var refCursor = (OracleRefCursor)messagesParameter.Value;
        await using DbDataReader reader = refCursor.GetDataReader();

        var batch = new List<OutgoingMessage>(settings.BatchSize);
        while (await reader.ReadAsync(cancellationToken))
        {
            batch.Add(new OutgoingMessage(
                MessageId: reader.GetGuid(0),
                MessageType: reader.GetString(1),
                Payload: reader.GetString(2),
                OccurredOnUtc: reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return batch;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {MessageType} message {MessageId} failed; it will be retried.")]
    private partial void LogPublishFailed(Exception exception, string messageType, Guid messageId);
}
