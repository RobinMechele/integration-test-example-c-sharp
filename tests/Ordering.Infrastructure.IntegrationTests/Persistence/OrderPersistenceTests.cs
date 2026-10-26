using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Domain.Customers;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.IntegrationTests.Persistence;

public sealed class OrderPersistenceTests(InfrastructureFixture fixture) : DatabaseTest(fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_order_and_its_lines_are_stored_and_loaded_without_losing_data()
    {
        var customer = await RegisterCustomerAsync();
        var order = Order.Place(customer.Id, [new NewOrderLine("BOOK-001", 2, 19.99m), new NewOrderLine("PEN-042", 3, 0.05m)], Now);
        await SaveAsync(order);

        var loaded = await LoadAsync(order.Id);

        loaded.ShouldNotBeNull();
        loaded.CustomerId.ShouldBe(customer.Id);
        loaded.Status.ShouldBe(OrderStatus.Placed);
        loaded.Total.ShouldBe(40.13m);
        loaded.PlacedAtUtc.ShouldBe(Now);
        loaded.Version.ShouldBe(1);
        loaded.Lines.Select(l => (l.Sku, l.Quantity, l.UnitPrice)).ShouldBe(
            [("BOOK-001", 2, 19.99m), ("PEN-042", 3, 0.05m)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_customers_orders_are_listed_most_recent_first()
    {
        var customer = await RegisterCustomerAsync();
        var otherCustomer = await RegisterCustomerAsync("other@example.com");
        var oldest = Order.Place(customer.Id, [new NewOrderLine("A", 1, 1m)], Now.AddDays(-2));
        var newest = Order.Place(customer.Id, [new NewOrderLine("B", 1, 1m)], Now);
        var middle = Order.Place(customer.Id, [new NewOrderLine("C", 1, 1m)], Now.AddDays(-1));
        var notMine = Order.Place(otherCustomer.Id, [new NewOrderLine("D", 1, 1m)], Now);
        await SaveAsync(oldest, newest, middle, notMine);

        var orders = await InNewScopeAsync(services =>
            services.GetRequiredService<IOrderRepository>().ListForCustomerAsync(customer.Id, CancellationToken));

        orders.Select(o => o.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
    }

    [Fact]
    public async Task Two_concurrent_changes_to_the_same_order_cannot_both_be_saved()
    {
        var customer = await RegisterCustomerAsync();
        var order = Order.Place(customer.Id, [new NewOrderLine("BOOK-001", 1, 10m)], Now);
        await SaveAsync(order);

        // Two "requests" load the same version of the order...
        await using var firstRequest = Fixture.Services.CreateAsyncScope();
        await using var secondRequest = Fixture.Services.CreateAsyncScope();
        var first = await firstRequest.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdAsync(order.Id, CancellationToken);
        var second = await secondRequest.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdAsync(order.Id, CancellationToken);

        // ...both change it; the first one to commit wins.
        first!.Cancel("Customer called", Now.AddHours(1));
        await firstRequest.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);

        second!.Cancel("Duplicate order", Now.AddHours(1));
        var exception = await Should.ThrowAsync<ConflictException>(() =>
            secondRequest.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken));

        exception.Code.ShouldBe("concurrency-conflict");
        var stored = await LoadAsync(order.Id);
        stored!.CancellationReason.ShouldBe("Customer called");
        stored.Version.ShouldBe(2);
    }

    private async Task<Customer> RegisterCustomerAsync(string email = "jane@example.com")
    {
        var customer = Customer.Register("Jane Doe", Email.Create(email), Now.AddDays(-30));
        await InNewScopeAsync(async services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
        });
        return customer;
    }

    private Task SaveAsync(params Order[] orders) =>
        InNewScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IOrderRepository>();
            foreach (var order in orders)
            {
                repository.Add(order);
            }

            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
        });

    private Task<Order?> LoadAsync(Guid orderId) =>
        InNewScopeAsync(services => services.GetRequiredService<IOrderRepository>().GetByIdAsync(orderId, CancellationToken));
}
