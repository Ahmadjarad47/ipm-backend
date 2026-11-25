namespace Ipm.Domain.Activity;

/// <summary>The fixed keys of notifications (spec 08 §2). They are text keys, not an enum, so a new kind is a new constant and nothing is renumbered.</summary>
public static class NotificationKinds
{
    public const string WorkItemAssigned = "work-item.assigned";
    public const string WorkItemUnassigned = "work-item.unassigned";
    public const string WorkItemStatusChanged = "work-item.status-changed";
    public const string ProjectMemberAdded = "project.member-added";
    public const string ProjectMemberRemoved = "project.member-removed";
    public const string ProjectStatusChanged = "project.status-changed";
    public const string CommentMentioned = "comment.mentioned";
    public const string ApprovalRequested = "approval.requested";
    public const string ApprovalDecided = "approval.decided";
    public const string ApprovalSuperseded = "approval.superseded";
    public const string ApprovalReminded = "approval.reminded";
    public const string ApprovalDelegated = "approval.delegated";
    public const string GitPrMerged = "git.pr-merged";

    /// <summary>Spec 11 R6: the kinds that also go to Telegram. Adding a kind here is all it takes to send more.</summary>
    public static readonly IReadOnlySet<string> TelegramDefault = new HashSet<string> { WorkItemAssigned };

    public static readonly IReadOnlyList<string> All =
    [
        WorkItemAssigned, WorkItemUnassigned, WorkItemStatusChanged, ProjectMemberAdded, ProjectMemberRemoved, ProjectStatusChanged,
        CommentMentioned, ApprovalRequested, ApprovalDecided, ApprovalSuperseded, ApprovalReminded, ApprovalDelegated, GitPrMerged,
    ];
}
