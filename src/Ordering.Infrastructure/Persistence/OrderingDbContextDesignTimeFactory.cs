using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Used by the 'dotnet ef' tools only. 'migrations add' does not connect to a database;
/// 'database update' uses the ORDERING_CONNECTION_STRING environment variable.
/// </summary>
internal sealed class OrderingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDERING_CONNECTION_STRING")
            ?? "User Id=ordering;Password=ordering;Data Source=localhost:1521/FREEPDB1";

        var options = new DbContextOptionsBuilder<OrderingDbContext>().UseOrderingOracle(connectionString).Options;
        return new OrderingDbContext(options);
    }
}
