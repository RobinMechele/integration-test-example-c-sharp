using System.Net;
using Ordering.Api.IntegrationTests.Infrastructure;

namespace Ordering.Api.IntegrationTests;

public sealed class HealthCheckTests(OrderingApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task The_health_endpoint_reports_healthy_when_Oracle_is_reachable()
    {
        var response = await Client.GetAsync("/health", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).ShouldBe("Healthy");
    }
}
