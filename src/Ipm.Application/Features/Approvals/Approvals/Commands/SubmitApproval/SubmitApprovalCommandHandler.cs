using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Exceptions;
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

        var sensitive = template.Event == ApprovalEvent.SensitiveChange;
        if (!sensitive && command.ProposedChanges is { Count: > 0 })
        {
            return ApprovalErrors.ProposedChangesNotAllowed; // 06b R1
        }

        var subject = (await ApprovalSubjects.LoadAsync(db, command.SubjectType, command.SubjectId, cancellationToken))!;
        if (subject.Item?.IsArchived == true && sensitive)
        {
            return ApprovalErrors.SubjectArchived; // 06b R1
        }

        // R4: the template must govern this subject by its own rule. A sensitive change is the action change.
        if (!(sensitive ? TemplateMatcher.Matches(template, subject, ApprovalAction.Change) : TemplateMatcher.Matches(template, subject)))
        {
            return ApprovalErrors.TemplateNotApplicable;
        }

        ApprovalAction action;
        if (sensitive)
        {
            if (command.Action is { } given && given != ApprovalAction.Change)
            {
                return ApprovalErrors.ActionMismatch;
            }

            action = ApprovalAction.Change;
        }
        else
        {
            action = command.Action ?? await InferActionAsync(template, subject, cancellationToken);
            if (action == ApprovalAction.Change || ApprovalSubjectFields.EventFor(subject.Type, action) != template.Event || !TemplateMatcher.Matches(template, subject, action))
            {
                return ApprovalErrors.ActionMismatch;
            }
        }

        if (command.TargetStatusId is { } target && !await db.Statuses.AnyAsync(s => s.Id == target, cancellationToken))
        {
            return Error.Validation("Approval.TargetStatusInvalid", "الحالة المستهدفة غير صالحة.");
        }

        var proposed = new List<SnapshotEntry>();
        if (sensitive)
        {
            var checkedChanges = await CheckProposedAsync(template, subject, caller, command.ProposedChanges, cancellationToken);
            if (checkedChanges.IsFailure)
            {
                return checkedChanges.Error;
            }

            proposed = checkedChanges.Value;
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
            action, command.TargetStatusId, snapshot, resolved, time.GetUtcNow(), proposedChanges: proposed);
        db.ApprovalRequests.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToResponse(time.GetUtcNow(), canDecide: false);
    }

    /// <summary>06b R1 to R5: the values are named, protected by the template, allowed to the requester, valid on the live data, and really different.</summary>
    private async Task<Result<List<SnapshotEntry>>> CheckProposedAsync(
        ApprovalTemplate template, ApprovalSubject subject, UserAccess caller, IReadOnlyDictionary<string, string?>? requested, CancellationToken cancellationToken)
    {
        if (requested is null || requested.Count == 0)
        {
            return ApprovalErrors.ProposedChangesRequired;
        }

        var notAllowed = SensitiveChanges.NotAllowed(template, subject.Type, requested.Keys);
        if (notAllowed.Count > 0)
        {
            return ApprovalErrors.FieldsNotProtected(notAllowed);
        }

        if (SensitiveChanges.MissingPermission(caller, subject.ProjectId, subject.Type, requested.Keys) is { } missing)
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: missing, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        var proposed = requested.Select(p => new SnapshotEntry(p.Key, ProposedValues.Normalize(p.Key, p.Value))).ToList();
        var (problems, _) = await SensitiveChanges.ValidateAsync(db, subject, proposed, cancellationToken);
        if (problems.Count > 0)
        {
            throw new ValidationException(problems.GroupBy(p => p.Path, p => p.Message).ToDictionary(g => g.Key, g => g.Distinct().ToArray()));
        }

        return await SensitiveChanges.IsNoChangeAsync(db, subject, proposed, cancellationToken) ? ApprovalErrors.NoChange : proposed;
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
