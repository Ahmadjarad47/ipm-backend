namespace Ipm.Application.Abstractions.Data;

/// <summary>
/// The tables handlers read and write, without knowing PostgreSQL. Each part adds its <c>DbSet</c>s here.
/// Writes are committed with <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IApplicationDbContext
{
}
