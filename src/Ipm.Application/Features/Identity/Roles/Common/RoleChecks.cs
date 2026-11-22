using Ipm.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Roles.Common;

/// <summary>Checks that need the database, shared by the role commands.</summary>
internal static class RoleChecks
{
    /// <summary>
    /// R17: role names are unique regardless of letter case. <paramref name="exceptId"/> is the role being renamed.
    /// A system has a handful of roles, so comparing in memory is cheaper than a provider-specific query.
    /// </summary>
    public static async Task<bool> NameTakenAsync(IApplicationDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var names = await db.Roles.AsNoTracking().Where(r => r.Id != exceptId).Select(r => r.Name).ToListAsync(cancellationToken);
        return names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }
}
