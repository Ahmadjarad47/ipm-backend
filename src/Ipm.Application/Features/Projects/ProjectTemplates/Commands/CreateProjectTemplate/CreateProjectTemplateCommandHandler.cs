using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Commands.CreateProjectTemplate;

internal sealed class CreateProjectTemplateCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProjectTemplateCommand, ProjectTemplateResponse>
{
    public async ValueTask<Result<ProjectTemplateResponse>> Handle(CreateProjectTemplateCommand command, CancellationToken cancellationToken)
    {
        if (await ProjectTemplateChecks.NameTakenAsync(db, command.Name, null, cancellationToken))
        {
            return ProjectTemplateErrors.NameTaken; // R15
        }

        var initial = await ProjectTemplateChecks.ValidateAsync(
            db, command.AllowedTypeIds, command.InitialStatusId, command.SuggestedRoleIds, command.SuggestedMembers, cancellationToken);
        if (initial.IsFailure)
        {
            return initial.Error; // R14
        }

        var created = ProjectTemplate.Create(
            command.Name, command.Description, command.Active, command.AllowedTypeIds, command.DefaultTypeId, initial.Value,
            command.SuggestedRoleIds, command.DurationDays, command.SuggestedMembers);
        if (created.IsFailure)
        {
            return created.Error; // R14
        }

        db.ProjectTemplates.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
