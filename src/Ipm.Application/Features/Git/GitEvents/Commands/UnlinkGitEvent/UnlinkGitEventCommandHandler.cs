using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.GitEvents.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.GitEvents.Commands.UnlinkGitEvent;

internal sealed class UnlinkGitEventCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<UnlinkGitEventCommand, GitEventResponse>
{
    public async ValueTask<Result<GitEventResponse>> Handle(UnlinkGitEventCommand command, CancellationToken cancellationToken)
    {
        var gitEvent = await db.GitEvents.SingleOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (gitEvent is null)
        {
            return GitErrors.EventNotFound;
        }

        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, gitEvent.ProjectId, Permissions.TasksEdit, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == Ipm.Domain.Projects.ProjectErrors.NotFound ? GitErrors.EventNotFound : guarded.Error;
        }

        gitEvent.Unlink(command.Ref.Trim().ToUpperInvariant());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return gitEvent.ToResponse();
    }
}
