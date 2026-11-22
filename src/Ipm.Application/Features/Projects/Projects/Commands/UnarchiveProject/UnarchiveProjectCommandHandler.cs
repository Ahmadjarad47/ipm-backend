using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Projects.Projects.Commands.UnarchiveProject;

internal sealed class UnarchiveProjectCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<UnarchiveProjectCommand, ProjectResponse>
{
    public async ValueTask<Result<ProjectResponse>> Handle(UnarchiveProjectCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.ProjectsArchive, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var project = guarded.Value.Project;
        if (!project.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        project.Unarchive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToResponse();
    }
}
