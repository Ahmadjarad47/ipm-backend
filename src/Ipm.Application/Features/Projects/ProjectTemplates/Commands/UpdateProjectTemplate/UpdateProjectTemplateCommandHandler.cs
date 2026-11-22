using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Commands.UpdateProjectTemplate;

internal sealed class UpdateProjectTemplateCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProjectTemplateCommand, ProjectTemplateResponse>
{
    public async ValueTask<Result<ProjectTemplateResponse>> Handle(UpdateProjectTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await db.ProjectTemplates.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (template is null)
        {
            return ProjectTemplateErrors.NotFound;
        }

        if (!template.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (await ProjectTemplateChecks.NameTakenAsync(db, command.Name, template.Id, cancellationToken))
        {
            return ProjectTemplateErrors.NameTaken; // R15
        }

        var initial = await ProjectTemplateChecks.ValidateAsync(
            db, command.AllowedTypeIds, command.InitialStatusId, command.SuggestedRoleIds, command.SuggestedMembers, cancellationToken);
        if (initial.IsFailure)
        {
            return initial.Error; // R14
        }

        var updated = template.Update(
            command.Name, command.Description, command.Active ?? template.IsActive, command.AllowedTypeIds, command.DefaultTypeId, initial.Value,
            command.SuggestedRoleIds, command.DurationDays, command.SuggestedMembers);
        if (updated.IsFailure)
        {
            return updated.Error; // R14
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToResponse();
    }
}
