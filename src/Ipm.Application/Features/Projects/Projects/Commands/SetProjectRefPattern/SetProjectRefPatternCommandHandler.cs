using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectRefPattern;

internal sealed class SetProjectRefPatternCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<SetProjectRefPatternCommand, ProjectResponse>
{
    public async ValueTask<Result<ProjectResponse>> Handle(SetProjectRefPatternCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.GitManage, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var project = guarded.Value.Project;
        if (!project.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (project.IsArchived)
        {
            return GitErrors.ProjectArchived;
        }

        var pattern = command.RefPattern?.Trim();
        if (!string.IsNullOrEmpty(pattern))
        {
            // It must recognise the project own refs, so a typo cannot silently stop all auto-linking.
            var samples = await db.WorkItems.AsNoTracking().Where(i => i.ProjectId == project.Id).OrderBy(i => i.Number).Select(i => i.Ref).Take(5).ToListAsync(cancellationToken);
            var problem = GitRefPatterns.Validate(pattern, samples.Count > 0 ? samples : [$"{project.Code}-101"]);
            if (problem is not null)
            {
                return GitErrors.RefPatternInvalid(problem);
            }
        }

        project.SetRefPattern(pattern);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToResponse();
    }
}
