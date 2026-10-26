using Ordering.Infrastructure.IntegrationTests;
using Xunit.Sdk;
using Xunit.v3;

// One Oracle container (and one service provider) for all tests in this assembly.
[assembly: AssemblyFixture(typeof(InfrastructureFixture))]

// The tests share that database and wipe it before each test, so they must run one at a time.
// (Different test assemblies still run in parallel: each one has its own container.)
[assembly: Parallelization(Mode = ParallelMode.None)]
