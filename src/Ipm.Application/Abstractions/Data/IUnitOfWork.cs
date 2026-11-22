namespace Ipm.Application.Abstractions.Data;

/// <summary>
/// Commits all changes of the current command in one database transaction.
/// Command handlers call it once, after the domain operation succeeded.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
