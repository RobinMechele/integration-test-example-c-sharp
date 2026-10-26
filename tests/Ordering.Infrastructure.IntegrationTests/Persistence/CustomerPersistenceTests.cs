using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;

namespace Ordering.Infrastructure.IntegrationTests.Persistence;

public sealed class CustomerPersistenceTests(InfrastructureFixture fixture) : DatabaseTest(fixture)
{
    // Sub-second precision on purpose: proves TIMESTAMP(7) WITH TIME ZONE keeps every tick.
    private static readonly DateTimeOffset RegisteredAt = new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero).AddTicks(1_234_567);

    [Fact]
    public async Task A_customer_is_stored_and_loaded_without_losing_data()
    {
        var customer = Customer.Register("Jane Doe", Email.Create("jane@example.com"), RegisteredAt);
        await SaveAsync(customer);

        var loaded = await InNewScopeAsync(services =>
            services.GetRequiredService<ICustomerRepository>().GetByIdAsync(customer.Id, CancellationToken));

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Jane Doe");
        loaded.Email.ShouldBe(Email.Create("jane@example.com"));
        loaded.RegisteredAtUtc.ShouldBe(RegisteredAt);
    }

    [Fact]
    public async Task The_unique_index_rejects_a_second_customer_with_the_same_email()
    {
        // Simulates two requests racing past the handler's "is the e-mail taken?" check:
        // only the database can stop the second one.
        await SaveAsync(Customer.Register("Jane Doe", Email.Create("jane@example.com"), RegisteredAt));

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            SaveAsync(Customer.Register("Other Jane", Email.Create("JANE@example.com"), RegisteredAt)));

        exception.Code.ShouldBe("customer.email.taken");
        (await QueryAsync(db => db.Customers.CountAsync(CancellationToken))).ShouldBe(1);
    }

    [Fact]
    public async Task Looking_up_an_email_ignores_the_casing_the_user_typed()
    {
        // Oracle compares strings case-sensitively; this works because Email normalises to lower case.
        await SaveAsync(Customer.Register("Jane Doe", Email.Create("Jane.Doe@Example.com"), RegisteredAt));

        var isTaken = await InNewScopeAsync(services =>
            services.GetRequiredService<ICustomerRepository>().IsEmailTakenAsync(Email.Create("JANE.DOE@EXAMPLE.COM"), CancellationToken));

        isTaken.ShouldBeTrue();
    }

    private Task SaveAsync(Customer customer) =>
        InNewScopeAsync(async services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
        });
}
