using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Common;

internal static class StatusMapping
{
    public static StatusResponse ToResponse(this Status status) => new(
        status.Id,
        status.Key,
        status.Name,
        status.Description,
        status.Color,
        status.Category,
        status.Scope,
        status.SortOrder,
        status.IsActive,
        status.Version);
}
