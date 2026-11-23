using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Common;

internal static class SettingsExtensions
{
    /// <summary>Spec 09 R5: the organization policies, read from the one settings row (the defaults when it is not there yet).</summary>
    public static async Task<SettingsValues> CurrentSettingsAsync(this IApplicationDbContext db, CancellationToken cancellationToken) =>
        (await db.Settings.AsNoTracking().SingleOrDefaultAsync(cancellationToken))?.Values() ?? SettingsValues.Defaults;
}
