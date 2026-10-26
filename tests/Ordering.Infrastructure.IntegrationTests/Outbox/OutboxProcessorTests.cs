using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ordering.Domain.Common;
using Ordering.Infrastructure.Outbox;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.IntegrationTests.Outbox;

/// <summary>
/// The relay side of the transactional outbox. The processor is called directly (no background loop, no sleeping),
/// which makes these tests fast and deterministic.
/// </summary>
public sealed class OutboxProcessorTests(InfrastructureFixture fixture) : DatabaseTest(fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pending_messages_are_published_in_the_order_they_occurred_and_marked_as_processed()
    {
        var second = await SeedAsync(new TestEvent("second", Start.AddSeconds(2)));
        var first = await SeedAsync(new TestEvent("first", Start));
        var third = await SeedAsync(new TestEvent("third", Start.AddSeconds(3)));

        var claimed = await ProcessAsync();

        claimed.ShouldBe(3);
        Fixture.MessagePublisher.Published.Select(m => m.MessageId).ShouldBe([first.Id, second.Id, third.Id]);
        var stored = await QueryAsync(db => db.OutboxMessages.ToListAsync(CancellationToken));
        stored.ShouldAllBe(m => m.ProcessedOnUtc == Fixture.Time.GetUtcNow() && m.AttemptCount == 1 && m.LastError == null);
    }

    [Fact]
    public async Task The_published_message_carries_the_stored_type_payload_and_timestamp()
    {
        var seeded = await SeedAsync(new TestEvent("hello", Start));

        await ProcessAsync();

        var published = Fixture.MessagePublisher.Published.ShouldHaveSingleItem();
        published.MessageId.ShouldBe(seeded.Id);
        published.MessageType.ShouldBe(nameof(TestEvent));
        published.Payload.ShouldBe(seeded.Payload);
        published.OccurredOnUtc.ShouldBe(Start);
    }

    [Fact]
    public async Task Messages_that_were_already_processed_are_not_published_again()
    {
        await SeedAsync(new TestEvent("only once", Start));

        (await ProcessAsync()).ShouldBe(1);
        (await ProcessAsync()).ShouldBe(0);

        Fixture.MessagePublisher.Published.Count.ShouldBe(1);
    }

    [Fact]
    public async Task One_run_claims_at_most_one_batch()
    {
        var seededIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            seededIds.Add((await SeedAsync(new TestEvent($"event {i}", Start.AddSeconds(i)))).Id);
        }

        (await ProcessAsync(batchSize: 2)).ShouldBe(2);
        (await ProcessAsync(batchSize: 2)).ShouldBe(2);
        (await ProcessAsync(batchSize: 2)).ShouldBe(1);
        (await ProcessAsync(batchSize: 2)).ShouldBe(0);

        Fixture.MessagePublisher.Published.Select(m => m.MessageId).ShouldBe(seededIds);
    }

    [Fact]
    public async Task A_message_that_fails_to_publish_is_retried_later_without_blocking_the_others()
    {
        var ok1 = await SeedAsync(new TestEvent("ok 1", Start));
        var failing = await SeedAsync(new TestEvent("broker down", Start.AddSeconds(1)));
        var ok2 = await SeedAsync(new TestEvent("ok 2", Start.AddSeconds(2)));
        Fixture.MessagePublisher.FailWhen(m => m.MessageId == failing.Id);

        await ProcessAsync();

        Fixture.MessagePublisher.Published.Select(m => m.MessageId).ShouldBe([ok1.Id, ok2.Id]);
        var afterFailure = await QueryAsync(db => db.OutboxMessages.SingleAsync(m => m.Id == failing.Id, CancellationToken));
        afterFailure.ProcessedOnUtc.ShouldBeNull();
        afterFailure.AttemptCount.ShouldBe(1);
        afterFailure.LastError.ShouldNotBeNull().ShouldContain("Simulated broker failure");

        // The broker recovers: the next run publishes the message that failed.
        Fixture.MessagePublisher.FailWhen(_ => false);
        await ProcessAsync();

        Fixture.MessagePublisher.Published.Select(m => m.MessageId).ShouldBe([ok1.Id, ok2.Id, failing.Id]);
        var afterRetry = await QueryAsync(db => db.OutboxMessages.SingleAsync(m => m.Id == failing.Id, CancellationToken));
        afterRetry.ProcessedOnUtc.ShouldNotBeNull();
        afterRetry.AttemptCount.ShouldBe(2);
        afterRetry.LastError.ShouldBeNull();
    }

    [Fact]
    public async Task A_message_is_no_longer_retried_after_the_maximum_number_of_attempts()
    {
        var poison = await SeedAsync(new TestEvent("always fails", Start));
        Fixture.MessagePublisher.FailWhen(_ => true);

        (await ProcessAsync(maxAttempts: 2)).ShouldBe(1);
        (await ProcessAsync(maxAttempts: 2)).ShouldBe(1);
        (await ProcessAsync(maxAttempts: 2)).ShouldBe(0);

        var stored = await QueryAsync(db => db.OutboxMessages.SingleAsync(m => m.Id == poison.Id, CancellationToken));
        stored.AttemptCount.ShouldBe(2);
        stored.ProcessedOnUtc.ShouldBeNull("it stays in the table for someone to investigate");
    }

    [Fact]
    public async Task Processors_running_at_the_same_time_never_publish_the_same_message_twice()
    {
        // Several app instances poll the same outbox table. FOR UPDATE SKIP LOCKED must hand each of them
        // different rows. Only a real database with real row locks can verify this.
        const int messageCount = 60;
        const int processorCount = 4;
        await using (var dbContext = Fixture.Database.CreateDbContext())
        {
            dbContext.OutboxMessages.AddRange(Enumerable.Range(0, messageCount)
                .Select(i => OutboxMessage.FromDomainEvent(new TestEvent($"event {i}", Start.AddMilliseconds(i)))));
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        // Slow publishing down so the processors really overlap.
        Fixture.MessagePublisher.SimulateLatency(TimeSpan.FromMilliseconds(10));
        var claimedPerProcessor = new ConcurrentDictionary<int, int>();

        await Task.WhenAll(Enumerable.Range(0, processorCount).Select(processor => Task.Run(
            async () =>
            {
                int claimed;
                do
                {
                    claimed = await ProcessAsync(batchSize: 5);
                    claimedPerProcessor.AddOrUpdate(processor, claimed, (_, total) => total + claimed);
                }
                while (claimed > 0);
            },
            CancellationToken)));

        var publishedIds = Fixture.MessagePublisher.Published.Select(m => m.MessageId).ToList();
        publishedIds.Count.ShouldBe(messageCount);
        publishedIds.Distinct().Count().ShouldBe(messageCount);
        claimedPerProcessor.Values.Count(claimed => claimed > 0).ShouldBeGreaterThan(1, "the work should have been shared");
        (await QueryAsync(db => db.OutboxMessages.CountAsync(m => m.ProcessedOnUtc == null, CancellationToken))).ShouldBe(0);
    }

    private async Task<OutboxMessage> SeedAsync(IDomainEvent domainEvent)
    {
        var message = OutboxMessage.FromDomainEvent(domainEvent);
        await using var dbContext = Fixture.Database.CreateDbContext();
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(CancellationToken);
        return message;
    }

    /// <summary>Runs one batch with a processor in its own scope (own DbContext and connection).</summary>
    private Task<int> ProcessAsync(int batchSize = 20, int maxAttempts = 5) =>
        InNewScopeAsync(services =>
        {
            var processor = new OutboxProcessor(
                services.GetRequiredService<OrderingDbContext>(),
                Fixture.MessagePublisher,
                Fixture.Time,
                Options.Create(new OutboxOptions { BatchSize = batchSize, MaxAttempts = maxAttempts }),
                NullLogger<OutboxProcessor>.Instance);

            return processor.ProcessAsync(CancellationToken);
        });

    private sealed record TestEvent(string Text, DateTimeOffset OccurredOnUtc) : IDomainEvent;
}
