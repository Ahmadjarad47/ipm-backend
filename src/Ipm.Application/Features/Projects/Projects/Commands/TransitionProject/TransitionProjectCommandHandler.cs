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

namespace Ipm.Application.Features.Projects.Projects.Commands.TransitionProject;

internal sealed class TransitionProjectCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<TransitionProjectCommand, ProjectResponse>
{
    private const string ProjectWorkflowKey = "project_default";

    public async ValueTask<Result<ProjectResponse>> Handle(TransitionProjectCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.ProjectsEdit, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (project, caller) = (guarded.Value.Project, guarded.Value.Caller);
        if (!project.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var editable = project.EnsureEditable(); // R6
        if (editable.IsFailure)
        {
            return editable.Error;
        }

        var workflow = await db.Workflows.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Key == ProjectWorkflowKey && w.Scope == StatusScope.Project, cancellationToken);
        if (workflow is null)
        {
            return ProjectErrors.WorkflowMissing;
        }

        var statuses = await db.Statuses.AsNoTracking()
            .Where(s => s.Id == project.StatusId || s.Id == command.ToStatusId)
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        if (!statuses.TryGetValue(command.ToStatusId, out var to) || !statuses.TryGetValue(project.StatusId, out var from))
        {
            return ProjectErrors.TransitionNotAllowed(statuses.GetValueOrDefault(project.StatusId)?.Name ?? "—", "—");
        }

        var transition = workflow.FindTransition(from.Id, to.Id);
        if (transition is null)
        {
            return ProjectErrors.TransitionNotAllowed(from.Name, to.Name); // R10
        }

        if (!to.IsActive)
        {
            return ProjectErrors.TargetStatusInactive; // R10
        }

        if (transition.Permission is { } permission && !caller.Can(permission, project.Id))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: permission, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden; // R10
        }

        // R9 of part 06: a move that needs an approval spends an approved request of the right event, once. Without one, only a holder of
        // audit.override may make it (and it is recorded).
        var overridden = false;
        if (transition.RequiresApproval)
        {
            var subject = new ApprovalSubject(ApprovalSubjectType.Project, project.Id, project, null);
            var gate = await ApprovalGate.FindAsync(db, subject, ApprovalGate.ProjectAction(to.Category), to.Id, time.GetUtcNow(), cancellationToken);
            if (gate.Request is { } approval)
            {
                approval.Consume(time.GetUtcNow());
            }
            else if (caller.Has(Permissions.AuditOverride))
            {
                overridden = true;
            }
            else
            {
                return gate.Drifted ? ApprovalErrors.ProtectedFieldsChanged : ProjectErrors.ApprovalRequired;
            }
        }

        project.MoveTo(to.Id);
        db.ProjectStatusChanges.Add(ProjectStatusChange.Create(project.Id, from.Id, to.Id, caller.UserId, command.Comment, time.GetUtcNow(), overridden));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToResponse();
    }
}
