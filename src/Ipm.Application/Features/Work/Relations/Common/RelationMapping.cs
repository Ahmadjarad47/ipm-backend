using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Relations.Common;

internal static class RelationMapping
{
    public static RelationResponse ToResponse(this WorkItemRelation relation) =>
        new(relation.Id, relation.SourceId, relation.TargetId, relation.RelationTypeId, relation.CreatedBy, relation.CreatedAt);
}
