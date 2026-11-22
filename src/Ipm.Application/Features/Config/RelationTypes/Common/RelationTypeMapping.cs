using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.RelationTypes.Common;

internal static class RelationTypeMapping
{
    public static RelationTypeResponse ToResponse(this RelationType type) => new(
        type.Id,
        type.Key,
        type.Name,
        type.InverseName,
        type.IsDirected,
        type.IsAcyclic,
        type.IsActive,
        type.AllowedPairs,
        type.Version);
}
