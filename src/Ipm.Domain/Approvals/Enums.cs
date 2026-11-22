namespace Ipm.Domain.Approvals;

/// <summary>What a template governs. Shown as kebab-case text (project-approve, project-close, work-item-approve, sensitive-change).</summary>
public enum ApprovalEvent
{
    ProjectApprove = 0,
    ProjectClose = 1,
    WorkItemApprove = 2,
    SensitiveChange = 3,
}

/// <summary>Who decides a stage: a user, a role (project or system), or a position resolved per request.</summary>
public enum ApproverKind
{
    User = 0,
    Role = 1,
    Position = 2,
}

/// <summary>Organization positions resolved for each request (R5). Shown as project-owner, direct-manager, project-manager.</summary>
public enum ApproverPosition
{
    ProjectOwner = 0,
    DirectManager = 1,
    ProjectManager = 2,
}

/// <summary>The approval state, separate from the status of the subject (FR-APR-13).</summary>
public enum ApprovalStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2,
    Returned = 3,
    Superseded = 4,
}

/// <summary>What a request or a rule is about: close, reopen, change (06b) or activate.</summary>
public enum ApprovalAction
{
    Close = 0,
    Reopen = 1,
    Change = 2,
    Activate = 3,
}

public enum ApprovalSubjectType
{
    Project = 0,
    WorkItem = 1,
}

/// <summary>A decision on a stage. Shown as approve, reject, return.</summary>
public enum DecisionKind
{
    Approve = 0,
    Reject = 1,
    Return = 2,
}
