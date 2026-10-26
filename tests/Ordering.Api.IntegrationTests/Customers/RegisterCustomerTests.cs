using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests.Customers;

public sealed class RegisterCustomerTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Registering_a_customer_returns_201_and_a_location_that_serves_the_new_customer()
    {
        var response = await Client.PostAsJsonAsync("/customers", new { name = "Jane Doe", email = "Jane.Doe@Example.com" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(CancellationToken);
        response.Headers.Location.ShouldBe(new Uri($"/customers/{created!.Id}", UriKind.Relative));

        var customer = await Client.GetFromJsonAsync<CustomerContract>(response.Headers.Location, CancellationToken);
        customer.ShouldBe(new CustomerContract(created.Id, "Jane Doe", "jane.doe@example.com", Factory.Time.GetUtcNow()));
    }

    [Fact]
    public async Task Registering_a_second_customer_with_the_same_email_returns_409()
    {
        await RegisterCustomerAsync(email: "jane@example.com");

        var response = await Client.PostAsJsonAsync("/customers", new { name = "Jane Again", email = "JANE@example.com" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken);
        problem!.Code.ShouldBe("customer.email.taken");
    }

    [Fact]
    public async Task Concurrent_registrations_with_the_same_email_create_exactly_one_customer()
    {
        // Requests racing each other can all pass the "is the e-mail taken?" check in the handler.
        // The unique index in Oracle is what guarantees the outcome; the API must still answer 409, not 500.
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            Client.PostAsJsonAsync("/customers", new { name = $"Jane {i}", email = "jane@example.com" }, CancellationToken)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(4);
        await using var dbContext = Factory.Database.CreateDbContext();
        (await dbContext.Customers.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Theory]
    [InlineData("", "jane@example.com", "name")]
    [InlineData("Jane Doe", "", "email")]
    [InlineData("Jane Doe", "not-an-email", "email")]
    public async Task An_invalid_registration_returns_400_naming_the_invalid_field_and_stores_nothing(string name, string email, string invalidField)
    {
        var response = await Client.PostAsJsonAsync("/customers", new { name, email }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken);
        problem!.Code.ShouldBe("validation-failed");
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe([invalidField]);

        await using var dbContext = Factory.Database.CreateDbContext();
        (await dbContext.Customers.CountAsync(CancellationToken)).ShouldBe(0);
    }
}
