using Ipm.Application.Abstractions.Caching;
using Ipm.Application.Abstractions.Settings;
using Ipm.Application.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Infrastructure.Persistence;

/// <summary>Spec 09 R5 and R6. A failing cache never stops a read (the database is the truth), and the time zone follows every load.</summary>
internal sealed class OrganizationSettings(ApplicationDbContext db, ICacheService cache) : IOrganizationSettings
{
    private const string Key = "settings:organization:v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public async Task<SettingsValues> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await cache.GetAsync<SettingsValues>(Key, cancellationToken) is { } cached)
            {
                RiyadhClock.Use(cached.TimeZone);
                return cached;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // fall through to the database
        }

        var values = await LoadAsync(cancellationToken);
        try
        {
            await cache.SetAsync(Key, values, Lifetime, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // the cache is only a speed-up
        }

        return values;
    }

    public async Task ChangedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(Key, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // the entry expires within a minute anyway
        }

        await LoadAsync(cancellationToken);
    }

    private async Task<SettingsValues> LoadAsync(CancellationToken cancellationToken)
    {
        var values = (await db.Settings.AsNoTracking().SingleOrDefaultAsync(cancellationToken))?.Values() ?? SettingsValues.Defaults;
        RiyadhClock.Use(values.TimeZone);
        return values;
    }
}
