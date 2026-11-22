using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Abstractions.Data;

/// <summary>
/// The tables handlers read and write, without knowing PostgreSQL. Each part adds its <c>DbSet</c>s here.
/// Writes are committed with <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IApplicationDbContext
{
    // Part 01: identity.
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }
}
