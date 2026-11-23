using Ipm.Domain.Common;

namespace Ipm.Domain.Approvals;

/// <summary>
/// One request for approval of an action on a project or a work item (FR-APR). The frozen stages, the decisions and the approvers resolved
/// at submission are columns of the request itself, so every decision moves its version (ETag) and shows in audit_log. A change of a
/// protected field supersedes a pending request (R11); an approved one authorizes exactly one action, once, for a limited time (R9).
/// </summary>
public sealed class ApprovalRequest : AuditableEntity, IAuditSubject
{
    Guid? IAuditSubject.AuditProjectId => ProjectId;

    Guid? IAuditSubject.AuditWorkItemId => SubjectType == ApprovalSubjectType.WorkItem ? SubjectId : null;

    string? IAuditSubject.AuditLabel => Title;


    public const int TitleMaxLength = 300;

    private ApprovalRequest() { }

    private ApprovalRequest(Guid id) : base(id) { }

    public Guid TemplateId { get; private set; }

    public ApprovalSubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid RequesterId { get; private set; }

    public ApprovalStatus Status { get; private set; }

    /// <summary>The active stage (1-based); 0 when the request is not pending.</summary>
    public int CurrentStage { get; private set; }

    public int Revision { get; private set; }

    public List<SnapshotEntry> Snapshot { get; private set; } = [];

    public List<ApprovalDecision> Decisions { get; private set; } = [];

    /// <summary>A copy of the template stages taken at submission: later edits of the template do not touch a running request.</summary>
    public List<ApprovalStage> Stages { get; private set; } = [];

    public List<ResolvedApprovers> ResolvedApprovers { get; private set; } = [];

    /// <summary>Sensitive changes only (06b): the values that are applied to the subject when the last stage approves. Empty otherwise.</summary>
    public List<SnapshotEntry> ProposedChanges { get; private set; } = [];

    public ApprovalAction? Action { get; private set; }

    public Guid? TargetStatusId { get; private set; }

    public DateTimeOffset? StageDueAt { get; private set; }

    public DateTimeOffset? LastRemindedAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>R4: starts pending on stage 1. The handler checked the template, the subject and that every stage has an approver.</summary>
    public static ApprovalRequest Create(
        ApprovalTemplate template, ApprovalSubjectType subjectType, Guid subjectId, Guid projectId, string title, Guid requesterId,
        ApprovalAction? action, Guid? targetStatusId, IEnumerable<SnapshotEntry> snapshot, IEnumerable<ResolvedApprovers> resolved, DateTimeOffset now, Guid? id = null, IEnumerable<SnapshotEntry>? proposedChanges = null)
    {
        var request = id is { } fixedId ? new ApprovalRequest(fixedId) : new ApprovalRequest();

        request.TemplateId = template.Id;
        request.SubjectType = subjectType;
        request.SubjectId = subjectId;
        request.ProjectId = projectId;
        request.Title = title.Length > TitleMaxLength ? title[..TitleMaxLength] : title;
        request.RequesterId = requesterId;
        request.Stages = [.. template.Stages];
        request.Action = action;
        request.TargetStatusId = targetStatusId;
        request.ProposedChanges = [.. proposedChanges ?? []];
        request.Start(snapshot, resolved, now, revision: 1);
        request.RaiseDomainEvent(new Ipm.Domain.Activity.Events.ApprovalStageOpened(request.Id));
        return request;
    }

    private void Start(IEnumerable<SnapshotEntry> snapshot, IEnumerable<ResolvedApprovers> resolved, DateTimeOffset now, int revision)
    {
        Status = ApprovalStatus.PendingApproval;
        CurrentStage = 1;
        Revision = revision;
        Snapshot = [.. snapshot];
        ResolvedApprovers = [.. resolved];
        StageDueAt = DueOf(1, now);
        LastRemindedAt = null;
    }

    public ApprovalStage? CurrentStageDefinition => Stages.Find(s => s.Order == CurrentStage);

    public IReadOnlyList<Guid> DirectApproversOf(int order) => ResolvedApprovers.Find(r => r.Order == order)?.UserIds ?? [];

    /// <summary>R6: the time the final approval was given, when approved.</summary>
    public DateTimeOffset? ApprovedAt => Status == ApprovalStatus.Approved ? Decisions[^1].At : null;

    /// <summary>R9: spent on its one action.</summary>
    public bool IsConsumed => ConsumedAt is not null;

    /// <summary>R9: an approval authorizes its action for <see cref="ApprovalPolicy.TtlDays"/> days only.</summary>
    public bool IsExpired(DateTimeOffset now) => ApprovedAt is { } at && now - at > TimeSpan.FromDays(ApprovalPolicy.TtlDays);

    /// <summary>R15: pending and past the due time of the active stage.</summary>
    public bool IsOverdue(DateTimeOffset now) => Status == ApprovalStatus.PendingApproval && StageDueAt is { } due && due < now;

    /// <summary>R6: a late decision on an old or superseded revision is refused, and so is a request that is not pending.</summary>
    public Result EnsureDecidable(int revision)
    {
        if (Status == ApprovalStatus.Superseded || revision != Revision)
        {
            return ApprovalErrors.Stale;
        }

        return Status == ApprovalStatus.PendingApproval ? Result.Success() : ApprovalErrors.NotPending;
    }

    /// <summary>R6, R7: records the decision and moves the request. The handler checked who may decide.</summary>
    public Result Decide(Guid deciderId, DecisionKind decision, string? comment, Guid? onBehalfOf, int revision, DateTimeOffset now)
    {
        var decidable = EnsureDecidable(revision);
        if (decidable.IsFailure)
        {
            return decidable;
        }

        if (decision != DecisionKind.Approve && string.IsNullOrWhiteSpace(comment))
        {
            return ApprovalErrors.CommentRequired;
        }

        Decisions = [.. Decisions, new ApprovalDecision(CurrentStage, deciderId, decision, comment?.Trim() ?? string.Empty, now, revision, onBehalfOf)];

        RaiseDomainEvent(new Ipm.Domain.Activity.Events.ApprovalDecided(Id, deciderId, decision.ToString()));

        switch (decision)
        {
            case DecisionKind.Approve when Stages.Exists(s => s.Order == CurrentStage + 1):
                CurrentStage++;
                StageDueAt = DueOf(CurrentStage, now);
                LastRemindedAt = null;
                RaiseDomainEvent(new Ipm.Domain.Activity.Events.ApprovalStageOpened(Id));
                break;
            case DecisionKind.Approve:
                Status = ApprovalStatus.Approved;
                CurrentStage = 0;
                StageDueAt = null;
                break;
            case DecisionKind.Reject:
                Status = ApprovalStatus.Rejected;
                CurrentStage = 0;
                StageDueAt = null;
                break;
            default:
                Status = ApprovalStatus.Returned;
                CurrentStage = 0;
                StageDueAt = null;
                break;
        }

        return Result.Success();
    }

    /// <summary>R7: the requester sends a returned or superseded request again: a new revision from stage 1.</summary>
    public Result Resubmit(IEnumerable<SnapshotEntry> snapshot, IEnumerable<ResolvedApprovers> resolved, DateTimeOffset now)
    {
        if (Status is not (ApprovalStatus.Returned or ApprovalStatus.Superseded))
        {
            return ApprovalErrors.NotResubmittable;
        }

        Start(snapshot, resolved, now, Revision + 1);
        RaiseDomainEvent(new Ipm.Domain.Activity.Events.ApprovalStageOpened(Id));
        return Result.Success();
    }

    /// <summary>R11: a protected field changed while the request was pending.</summary>
    public bool Supersede()
    {
        if (Status != ApprovalStatus.PendingApproval)
        {
            return false;
        }

        Status = ApprovalStatus.Superseded;
        CurrentStage = 0;
        StageDueAt = null;
        RaiseDomainEvent(new Ipm.Domain.Activity.Events.ApprovalSuperseded(Id));
        return true;
    }

    /// <summary>06b R6: after the proposed values were applied, the frozen copy shows the new values.</summary>
    public void RefreshSnapshot(IEnumerable<SnapshotEntry> snapshot) => Snapshot = [.. snapshot];

    /// <summary>R9: the approval is spent by its one action.</summary>
    public void Consume(DateTimeOffset now) => ConsumedAt = now;

    /// <summary>R14: at most one reminder an hour, and only while pending.</summary>
    public Result Remind(DateTimeOffset now)
    {
        if (Status != ApprovalStatus.PendingApproval)
        {
            return ApprovalErrors.NotPending;
        }

        if (LastRemindedAt is { } last && now - last < ApprovalPolicy.ReminderInterval)
        {
            return ApprovalErrors.RemindTooSoon;
        }

        LastRemindedAt = now;
        RaiseDomainEvent(new Ipm.Domain.Activity.Events.ApprovalReminded(Id));
        return Result.Success();
    }

    private DateTimeOffset? DueOf(int order, DateTimeOffset now) =>
        Stages.Find(s => s.Order == order)?.DueHours is { } hours ? now.AddHours(hours) : null;

}
