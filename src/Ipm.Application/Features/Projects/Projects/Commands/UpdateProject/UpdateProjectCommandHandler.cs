using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Commands.UpdateProject;

internal sealed class UpdateProjectCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<UpdateProjectCommand, ProjectResponse>
{
    public async ValueTask<Result<ProjectResponse>> Handle(UpdateProjectCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.ProjectsEdit, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var project = guarded.Value.Project;
        if (!project.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var editable = project.EnsureEditable(); // R6
        if (editable.IsFailure)
        {
            return editable.Error;
        }

        var codeUnchanged = project.EnsureCodeUnchanged(command.Code); // R1
        if (codeUnchanged.IsFailure)
        {
            return codeUnchanged.Error;
        }

        var before = (project.Name, project.Description, project.StartDate, project.EndDate, project.OwnerId);
        if (command.StatusId is { } statusId && statusId != project.StatusId)
        {
            return ProjectErrors.StatusChangeNotAllowed; // R9
        }

        if (command.OwnerId is { } ownerId && ownerId != project.OwnerId)
        {
            var owner = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == ownerId, cancellationToken);
            if (owner is not { IsActive: true })
            {
                return ProjectErrors.OwnerInvalid; // R3
            }

            var changed = project.ChangeOwner(ownerId);
            if (changed.IsFailure)
            {
                return changed.Error; // R3
            }
        }

        var updated = project.Update(command.Name, command.Description, command.StartDate, command.EndDate); // R2
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        // R11 of part 06: a pending approval that protects a field changed here is superseded in the same save.
        var changedFields = new List<string>();
        if (project.Name != before.Name) { changedFields.Add("name"); }
        if (project.Description != before.Description) { changedFields.Add("description"); }
        if (project.StartDate != before.StartDate) { changedFields.Add("startDate"); }
        if (project.EndDate != before.EndDate) { changedFields.Add("endDate"); }
        if (project.OwnerId != before.OwnerId) { changedFields.Add("ownerId"); }
        var direct = await SensitiveChangeGuard.EnsureAsync(db, guarded.Value.Caller, ApprovalSubjectType.Project, project.Id, changedFields, cancellationToken); // 06b R8
        if (direct.IsFailure)
        {
            return direct.Error;
        }

        await ApprovalSupersession.SupersedeAsync(db, ApprovalSubjectType.Project, project.Id, changedFields, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToResponse();
    }
}
