using System.Net;
using System.Net.Http.Json;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests.Customers;

public sealed class GetCustomerTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Getting_an_unknown_customer_returns_404()
    {
        var response = await Client.GetAsync($"/customers/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemContract>(CancellationToken);
        problem!.Code.ShouldBe("not-found");
    }
}
