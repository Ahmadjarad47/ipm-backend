using Ipm.Domain.Approvals;

namespace Ipm.Domain.UnitTests.Approvals;

public sealed class ApprovalRequestTests
{
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static ApprovalRequest NewRequest(int? dueHours = null)
    {
        var template = ApprovalTemplate.Create(
            "قالب", ApprovalEvent.WorkItemApprove, true,
            [new ApprovalStage(1, "أولى", ApproverKind.Role, Guid.NewGuid().ToString(), dueHours), new ApprovalStage(2, "ثانية", ApproverKind.User, Guid.NewGuid().ToString(), null)],
            ["title"], null, false).Value;
        return ApprovalRequest.Create(
            template, ApprovalSubjectType.WorkItem, Guid.NewGuid(), Guid.NewGuid(), "عنوان", Requester, ApprovalAction.Close, null,
            [new SnapshotEntry("title", "x")], [new ResolvedApprovers(1, [Approver]), new ResolvedApprovers(2, [Approver])], Start);
    }

    [Fact]
    public void R4_a_new_request_is_pending_on_stage_one_with_a_due_time_when_the_stage_has_one()
    {
        var request = NewRequest(dueHours: 24);

        Assert.Equal(ApprovalStatus.PendingApproval, request.Status);
        Assert.Equal(1, request.CurrentStage);
        Assert.Equal(1, request.Revision);
        Assert.Equal(Start.AddHours(24), request.StageDueAt);
        Assert.False(request.IsOverdue(Start.AddHours(23)));
        Assert.True(request.IsOverdue(Start.AddHours(25)));
    }

    [Fact]
    public void R6_approving_moves_through_the_stages_and_the_last_one_approves_the_request()
    {
        var request = NewRequest();

        Assert.True(request.Decide(Approver, DecisionKind.Approve, null, null, 1, Start.AddHours(1)).IsSuccess);
        Assert.Equal(2, request.CurrentStage);
        Assert.True(request.Decide(Approver, DecisionKind.Approve, "تمام", null, 1, Start.AddHours(2)).IsSuccess);

        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal(0, request.CurrentStage);
        Assert.Equal(Start.AddHours(2), request.ApprovedAt);
        Assert.Equal("Approval.NotPending", request.Decide(Approver, DecisionKind.Approve, null, null, 1, Start.AddHours(3)).Error.Code);
    }

    [Fact]
    public void R6_a_rejection_or_a_return_needs_a_comment_and_a_stale_revision_is_refused()
    {
        var request = NewRequest();

        Assert.Equal("Approval.CommentRequired", request.Decide(Approver, DecisionKind.Reject, " ", null, 1, Start).Error.Code);
        Assert.Equal("Approval.Stale", request.Decide(Approver, DecisionKind.Approve, null, null, 2, Start).Error.Code);
        Assert.True(request.Decide(Approver, DecisionKind.Return, "أكمل", null, 1, Start).IsSuccess);
        Assert.Equal(ApprovalStatus.Returned, request.Status);
    }

    [Fact]
    public void R7_only_a_returned_or_superseded_request_is_resent_and_it_starts_a_new_revision()
    {
        var request = NewRequest();
        Assert.Equal("Approval.NotResubmittable", request.Resubmit([], [new ResolvedApprovers(1, [Approver])], Start).Error.Code);

        Assert.True(request.Supersede());
        Assert.Equal(ApprovalStatus.Superseded, request.Status);
        Assert.Equal("Approval.Stale", request.EnsureDecidable(1).Error.Code);

        Assert.True(request.Resubmit([new SnapshotEntry("title", "y")], [new ResolvedApprovers(1, [Approver]), new ResolvedApprovers(2, [Approver])], Start.AddDays(1)).IsSuccess);
        Assert.Equal(2, request.Revision);
        Assert.Equal(1, request.CurrentStage);
        Assert.Equal(ApprovalStatus.PendingApproval, request.Status);
        Assert.False(request.Supersede() && request.Status == ApprovalStatus.PendingApproval);
    }

    [Fact]
    public void R9_an_approval_expires_after_fourteen_days_and_is_spent_once()
    {
        var request = NewRequest();
        request.Decide(Approver, DecisionKind.Approve, null, null, 1, Start);
        request.Decide(Approver, DecisionKind.Approve, null, null, 1, Start);

        Assert.False(request.IsExpired(Start.AddDays(14)));
        Assert.True(request.IsExpired(Start.AddDays(14).AddMinutes(1)));
        Assert.False(request.IsConsumed);
        request.Consume(Start.AddDays(1));
        Assert.True(request.IsConsumed);
    }

    [Fact]
    public void R14_a_reminder_waits_an_hour_and_only_a_pending_request_gets_one()
    {
        var request = NewRequest();

        Assert.True(request.Remind(Start).IsSuccess);
        Assert.Equal("Approval.RemindTooSoon", request.Remind(Start.AddMinutes(59)).Error.Code);
        Assert.True(request.Remind(Start.AddMinutes(61)).IsSuccess);
    }

    [Fact]
    public void R1_the_template_stages_must_be_sequential_with_valid_approvers_and_no_repeats_in_a_row()
    {
        var user = Guid.NewGuid().ToString();
        ApprovalStage Stage(int order, string id, ApproverKind kind = ApproverKind.User) => new(order, "مرحلة", kind, id, null);

        Assert.Empty(ApprovalTemplateDefinition.Validate("قالب", ApprovalEvent.ProjectClose, [Stage(1, user), Stage(2, "project-owner", ApproverKind.Position)], ["endDate"], null));
        Assert.NotEmpty(ApprovalTemplateDefinition.Validate("قالب", ApprovalEvent.ProjectClose, [Stage(1, user), Stage(3, Guid.NewGuid().ToString())], ["endDate"], null));
        Assert.NotEmpty(ApprovalTemplateDefinition.Validate("قالب", ApprovalEvent.ProjectClose, [Stage(1, user), Stage(2, user)], ["endDate"], null));
        Assert.NotEmpty(ApprovalTemplateDefinition.Validate("قالب", ApprovalEvent.ProjectClose, [Stage(1, "boss", ApproverKind.Position)], ["endDate"], null));
        Assert.NotEmpty(ApprovalTemplateDefinition.Validate("قالب", ApprovalEvent.ProjectClose, [Stage(1, user)], ["title"], null));
    }

    [Fact]
    public void R6_the_delegations_in_effect_decide_inside_their_dates_only()
    {
        var delegation = Delegation.Create(Approver, Guid.NewGuid(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10), "إجازة", new DateOnly(2026, 3, 1)).Value;

        Assert.True(delegation.IsInEffect(new DateOnly(2026, 3, 10)));
        Assert.False(delegation.IsInEffect(new DateOnly(2026, 3, 11)));
        Assert.True(delegation.Overlaps(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20)));
        Assert.True(delegation.Revoke().IsSuccess);
        Assert.False(delegation.IsInEffect(new DateOnly(2026, 3, 5)));
        Assert.Equal("Delegation.AlreadyRevoked", delegation.Revoke().Error.Code);
    }
}
