using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Infrastructure.IntegrationTests.Outbox;

/// <summary>
/// The write side of the transactional outbox: domain events must be stored in the same transaction
/// as the change that raised them. Transactions are exactly what an in-memory database cannot test.
/// </summary>
public sealed class OutboxWriteTests(InfrastructureFixture fixture) : DatabaseTest(fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Saving_an_aggregate_stores_its_domain_events_as_outbox_messages()
    {
        var customer = Customer.Register("Jane Doe", Email.Create("jane@example.com"), Now);

        await SaveAsync(customer);

        var message = (await QueryAsync(db => db.OutboxMessages.ToListAsync(CancellationToken))).ShouldHaveSingleItem();
        message.MessageType.ShouldBe("CustomerRegistered");
        message.OccurredOnUtc.ShouldBe(Now);
        message.ProcessedOnUtc.ShouldBeNull();
        message.AttemptCount.ShouldBe(0);

        using var payload = JsonDocument.Parse(message.Payload);
        payload.RootElement.GetProperty("customerId").GetGuid().ShouldBe(customer.Id);
        payload.RootElement.GetProperty("email").GetString().ShouldBe("jane@example.com");
    }

    [Fact]
    public async Task When_the_transaction_is_rolled_back_neither_the_data_nor_the_outbox_message_is_stored()
    {
        await using (var dbContext = Fixture.Database.CreateDbContext())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(CancellationToken);
            dbContext.Customers.Add(Customer.Register("Jane Doe", Email.Create("jane@example.com"), Now));
            await dbContext.SaveChangesAsync(CancellationToken);

            // Inside the transaction both rows exist...
            (await dbContext.OutboxMessages.CountAsync(CancellationToken)).ShouldBe(1);

            await transaction.RollbackAsync(CancellationToken);
        }

        // ...after the rollback, neither does.
        (await QueryAsync(db => db.Customers.CountAsync(CancellationToken))).ShouldBe(0);
        (await QueryAsync(db => db.OutboxMessages.CountAsync(CancellationToken))).ShouldBe(0);
    }

    [Fact]
    public async Task A_save_that_fails_in_the_database_does_not_leave_an_outbox_message_behind()
    {
        await SaveAsync(Customer.Register("Jane Doe", Email.Create("jane@example.com"), Now));

        // Violates the unique e-mail index, so Oracle rolls back the whole SaveChanges.
        await Should.ThrowAsync<ConflictException>(() =>
            SaveAsync(Customer.Register("Jane Again", Email.Create("jane@example.com"), Now)));

        var messages = await QueryAsync(db => db.OutboxMessages.ToListAsync(CancellationToken));
        messages.ShouldHaveSingleItem().MessageType.ShouldBe("CustomerRegistered");
    }

    [Fact]
    public async Task Saving_again_does_not_store_the_same_events_twice()
    {
        await InNewScopeAsync(async services =>
        {
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            services.GetRequiredService<ICustomerRepository>().Add(Customer.Register("Jane Doe", Email.Create("jane@example.com"), Now));

            await unitOfWork.SaveChangesAsync(CancellationToken);
            await unitOfWork.SaveChangesAsync(CancellationToken);
        });

        (await QueryAsync(db => db.OutboxMessages.CountAsync(CancellationToken))).ShouldBe(1);
    }

    [Fact]
    public async Task Large_payloads_are_stored_without_being_truncated()
    {
        // Oracle's EF Core provider maps an unbounded string to NVARCHAR2(2000) by default.
        // Event payloads can be larger than that, so PAYLOAD must be a LOB column.
        var largePayload = JsonSerializer.Serialize(new { data = new string('x', 10_000) });
        await using (var dbContext = Fixture.Database.CreateDbContext())
        {
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                MessageType = "LargeEvent",
                Payload = largePayload,
                OccurredOnUtc = Now,
            });
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        var stored = await QueryAsync(db => db.OutboxMessages.SingleAsync(CancellationToken));

        stored.Payload.ShouldBe(largePayload);
    }

    private Task SaveAsync(Customer customer) =>
        InNewScopeAsync(async services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
        });
}
