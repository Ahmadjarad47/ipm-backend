using Ipm.Domain.Common;

namespace Ipm.Domain.Work;

/// <summary>
/// A link from one work item (the source) to another (the target) of a relation type. It is stored in one direction only; the
/// other side is read through the inverse name of the type. Never edited: to change it, delete it and create another (R1).
/// </summary>
public sealed class WorkItemRelation : AuditableEntity
{
    private WorkItemRelation() { }

    private WorkItemRelation(Guid id) : base(id) { }

    public Guid SourceId { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid RelationTypeId { get; private set; }

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent. The other rules are checked by the handler.</summary>
    public static Result<WorkItemRelation> Create(Guid sourceId, Guid targetId, Guid relationTypeId, Guid? id = null)
    {
        if (sourceId == targetId)
        {
            return RelationErrors.SelfLink;
        }

        var relation = id is { } fixedId ? new WorkItemRelation(fixedId) : new WorkItemRelation();
        relation.SourceId = sourceId;
        relation.TargetId = targetId;
        relation.RelationTypeId = relationTypeId;
        return relation;
    }
}
