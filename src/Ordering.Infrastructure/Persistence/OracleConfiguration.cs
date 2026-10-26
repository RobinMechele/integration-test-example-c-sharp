using Microsoft.EntityFrameworkCore;

namespace Ordering.Infrastructure.Persistence;

public static class OracleConfiguration
{
    public const string ConnectionStringName = "Ordering";

    /// <summary>
    /// The single place where the Oracle provider is configured. Used at runtime, at design time
    /// (dotnet ef) and by the tests, so all three always produce the same model and SQL.
    /// </summary>
    public static DbContextOptionsBuilder UseOrderingOracle(this DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseOracle(connectionString, oracle => oracle
                // Generate SQL that also runs on Oracle 19c, the most common production version,
                // even though the tests run against Oracle Database Free 23.
                .UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)
                .MigrationsHistoryTable("EF_MIGRATIONS_HISTORY"))
            // Oracle folds unquoted identifiers to UPPER CASE. Using UPPER_SNAKE_CASE names means tables and
            // columns can be queried from SQL*Plus/SQL Developer without quoting ("ORDERS" instead of "Orders").
            .UseUpperSnakeCaseNamingConvention();

    /// <inheritdoc cref="UseOrderingOracle(DbContextOptionsBuilder, string)"/>
    public static DbContextOptionsBuilder<TContext> UseOrderingOracle<TContext>(this DbContextOptionsBuilder<TContext> options, string connectionString)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)options).UseOrderingOracle(connectionString);
        return options;
    }
}
