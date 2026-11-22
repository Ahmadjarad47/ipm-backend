using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.SubmitApproval;

internal sealed class SubmitApprovalCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<SubmitApprovalCommand, ApprovalResponse>
{
    public async ValueTask<Result<ApprovalResponse>> Handle(SubmitApprovalCommand command, CancellationToken cancellationToken)
    {
        var projectId = await ApprovalSubjects.ProjectIdAsync(db, command.SubjectType, command.SubjectId, cancellationToken);
        if (projectId is null)
        {
            return ApprovalErrors.SubjectNotFound;
        }

        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, projectId.Value, Permissions.ApprovalsRequest, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == ProjectErrors.NotFound ? ApprovalErrors.SubjectNotFound : guarded.Error;
        }

        var (project, caller) = (guarded.Value.Project, guarded.Value.Caller);
        if (project.IsArchived)
        {
            return ApprovalErrors.ProjectArchived;
        }

        var template = await db.ApprovalTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.TemplateId, cancellationToken);
        if (template is null)
        {
            return ApprovalErrors.TemplateNotFound;
        }

        if (!template.IsActive)
        {
            return ApprovalErrors.TemplateInactive;
        }

        if (template.Event == ApprovalEvent.SensitiveChange)
        {
            return ApprovalErrors.SensitiveNotSupported; // part 06b
        }

        var subject = (await ApprovalSubjects.LoadAsync(db, command.SubjectType, command.SubjectId, cancellationToken))!;
        if (!TemplateMatcher.Matches(template, subject))
        {
            return ApprovalErrors.TemplateNotApplicable; // R4: the template must govern this subject by its own rule
        }

        var action = command.Action ?? await InferActionAsync(template, subject, cancellationToken);
        if (action == ApprovalAction.Change || ApprovalSubjectFields.EventFor(subject.Type, action) != template.Event || !TemplateMatcher.Matches(template, subject, action))
        {
            return ApprovalErrors.ActionMismatch;
        }

        if (command.TargetStatusId is { } target && !await db.Statuses.AnyAsync(s => s.Id == target, cancellationToken))
        {
            return Error.Validation("Approval.TargetStatusInvalid", "الحالة المستهدفة غير صالحة.");
        }

        if (await db.ApprovalRequests.AnyAsync(
                r => r.SubjectType == command.SubjectType && r.SubjectId == command.SubjectId && r.TemplateId == template.Id && r.Status == ApprovalStatus.PendingApproval,
                cancellationToken))
        {
            return ApprovalErrors.AlreadyPending;
        }

        var (resolved, unresolved) = await ApproverResolver.FreezeAsync(db, template.Stages, project, caller.UserId, cancellationToken);
        if (unresolved is not null)
        {
            return ApprovalErrors.StageUnresolvable(unresolved.Order, unresolved.Name); // R4
        }

        var snapshot = await ApprovalSubjects.SnapshotAsync(db, subject, template.ProtectedFields, cancellationToken);
        var request = ApprovalRequest.Create(
            template, command.SubjectType, command.SubjectId, project.Id, $"{template.Name}: {subject.Label}", caller.UserId,
            action, command.TargetStatusId, snapshot, resolved, time.GetUtcNow());
        db.ApprovalRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToResponse(time.GetUtcNow(), canDecide: false);
    }

    /// <summary>R4: a project move to a done status closes it and any other activates it; an item that is done or cancelled is reopened, any other is closed.</summary>
    private async Task<ApprovalAction> InferActionAsync(ApprovalTemplate template, ApprovalSubject subject, CancellationToken cancellationToken)
    {
        if (subject.Item is null)
        {
            return template.Event == ApprovalEvent.ProjectClose ? ApprovalAction.Close : ApprovalAction.Activate;
        }

        var category = await db.Statuses.AsNoTracking().Where(s => s.Id == subject.Item.StatusId).Select(s => s.Category).SingleAsync(cancellationToken);
        return ApprovalGate.ItemAction(category);
    }
}
