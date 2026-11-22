namespace Ipm.Domain.Approvals;

/// <summary>The fields a template may protect, per kind of subject (R1). The same names are used by the snapshot and by the supersede hooks (R11).</summary>
public static class ApprovalSubjectFields
{
    public static readonly IReadOnlyList<string> Project = ["name", "description", "startDate", "endDate", "ownerId", "members"];

    public static readonly IReadOnlyList<string> WorkItem =
        ["title", "description", "dueDate", "startDate", "parentId", "priority", "typeId", "assigneeIds", "relations"];

    public static IReadOnlyList<string> For(ApprovalSubjectType type) => type == ApprovalSubjectType.Project ? Project : WorkItem;

    /// <summary>The kind of subject a template is for, or null when the fields fit neither (sensitive changes decide by their fields).</summary>
    public static ApprovalSubjectType? SubjectOf(ApprovalEvent approvalEvent, IReadOnlyCollection<string> protectedFields) => approvalEvent switch
    {
        ApprovalEvent.ProjectApprove or ApprovalEvent.ProjectClose => ApprovalSubjectType.Project,
        ApprovalEvent.WorkItemApprove => ApprovalSubjectType.WorkItem,
        _ when protectedFields.Count > 0 && protectedFields.All(Project.Contains) => ApprovalSubjectType.Project,
        _ when protectedFields.Count > 0 && protectedFields.All(WorkItem.Contains) => ApprovalSubjectType.WorkItem,
        _ => null,
    };

    /// <summary>The event an action needs on a kind of subject (R4).</summary>
    public static ApprovalEvent EventFor(ApprovalSubjectType type, ApprovalAction action) => action switch
    {
        ApprovalAction.Change => ApprovalEvent.SensitiveChange,
        _ when type == ApprovalSubjectType.Project => action == ApprovalAction.Close ? ApprovalEvent.ProjectClose : ApprovalEvent.ProjectApprove,
        _ => ApprovalEvent.WorkItemApprove,
    };
}
