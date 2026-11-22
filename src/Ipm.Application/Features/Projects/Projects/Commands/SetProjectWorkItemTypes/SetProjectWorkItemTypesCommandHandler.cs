using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkItemTypes;

internal sealed class SetProjectWorkItemTypesCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<SetProjectWorkItemTypesCommand, ProjectResponse>
{
    public async ValueTask<Result<ProjectResponse>> Handle(SetProjectWorkItemTypesCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(
            db, currentUser, access, securityLog, command.Id, Permissions.ProjectWorkItemTypesConfigure, cancellationToken);
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

        // R12: every allowed type exists, and the default one is switched on.
        var allowed = command.AllowedTypeIds.Distinct().ToList();
        var types = await db.WorkItemTypes.AsNoTracking().Where(t => allowed.Contains(t.Id)).Select(t => new { t.Id, t.IsActive }).ToListAsync(cancellationToken);
        if (types.Count != allowed.Count || types.Find(t => t.Id == command.DefaultTypeId) is { IsActive: false })
        {
            return ProjectErrors.TypesInvalid;
        }

        // R17 (part 04, enforced from part 05): a type with items in this project cannot be taken off the allowed list.
        var removedTypes = project.AllowedTypeIds.Where(id => !allowed.Contains(id)).ToList();
        var stranded = await WorkItemUsage.OfTypesAsync(db, project.Id, removedTypes, cancellationToken);
        if (stranded > 0)
        {
            return ProjectErrors.TypesInUse(stranded);
        }

        var set = project.SetAllowedTypes(allowed, command.DefaultTypeId);
        if (set.IsFailure)
        {
            return set.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToResponse();
    }
}
