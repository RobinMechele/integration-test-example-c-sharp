namespace Ordering.Application.Abstractions;

/// <summary>
/// Commits all changes made through the repositories in a single database transaction.
/// The outbox messages for raised domain events are written in that same transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="Exceptions.ConflictException">
    /// A unique constraint was violated or the data was changed by someone else in the meantime.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
