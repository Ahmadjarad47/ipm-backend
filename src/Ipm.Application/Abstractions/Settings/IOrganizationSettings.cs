using Ipm.Domain.Config;

namespace Ipm.Application.Abstractions.Settings;

/// <summary>Spec 09 R5 and R6: the organization policies as the rest of the system reads them, from a short cache (Redis, 60 seconds) over the database.</summary>
public interface IOrganizationSettings
{
    Task<SettingsValues> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>After a change: drops the cache and applies the time zone at once on this instance.</summary>
    Task ChangedAsync(CancellationToken cancellationToken = default);
}
