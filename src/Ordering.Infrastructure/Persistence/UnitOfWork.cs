using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Ordering.Application.Abstractions;
using Ordering.Application.Exceptions;
using Ordering.Infrastructure.Persistence.Configurations;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Commits the DbContext and translates database-specific failures into application exceptions,
/// so the Application layer never has to know about EF Core or Oracle error numbers.
/// </summary>
internal sealed class UnitOfWork(OrderingDbContext dbContext) : IUnitOfWork
{
    // ORA-00001: unique constraint violated.
    private const int UniqueConstraintViolated = 1;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(
                "concurrency-conflict",
                "The data was changed by another request. Reload it and try again.",
                exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is OracleException { Number: UniqueConstraintViolated } oracle)
        {
            var code = oracle.Message.Contains(CustomerConfiguration.UniqueEmailIndexName, StringComparison.OrdinalIgnoreCase)
                ? "customer.email.taken"
                : "unique-constraint-violation";

            throw new ConflictException(code, "The data conflicts with existing data.", exception);
        }
    }
}
