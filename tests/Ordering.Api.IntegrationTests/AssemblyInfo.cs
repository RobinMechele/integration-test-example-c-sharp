using Ordering.Api.IntegrationTests.Infrastructure;
using Xunit.Sdk;
using Xunit.v3;

// One Oracle container + one in-memory test server for all tests in this assembly.
[assembly: AssemblyFixture(typeof(OrderingApiFactory))]

// The tests share the database and reset it before each test, so they run one at a time.
[assembly: Parallelization(Mode = ParallelMode.None)]
