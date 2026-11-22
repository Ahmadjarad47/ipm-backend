using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Commands.CreateProject;

internal sealed class CreateProjectCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<CreateProjectCommand, ProjectResponse>
{
    private const string ProjectWorkflowKey = "project_default";
    private const string ManagerRoleKey = "project_manager";
    private const string DefaultTypeKey = "task";

    public async ValueTask<Result<ProjectResponse>> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var code = Project.NormalizeCode(command.Code);
        if (!Project.IsValidCode(code))
        {
            return ProjectErrors.CodeInvalid; // R1
        }

        if (await db.Projects.AnyAsync(p => p.Code == code, cancellationToken))
        {
            return ProjectErrors.CodeTaken; // R1
        }

        var owner = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == command.OwnerId, cancellationToken);
        if (owner is not { IsActive: true })
        {
            return ProjectErrors.OwnerInvalid; // R3
        }

        // R4: a project always starts in the initial status of the project workflow, never in a status it picked.
        var workflow = await db.Workflows.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Key == ProjectWorkflowKey && w.Scope == StatusScope.Project, cancellationToken);
        if (workflow is null)
        {
            return ProjectErrors.WorkflowMissing;
        }

        var managerRole = await db.Roles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Key == ManagerRoleKey && r.Scope == RoleScope.Project, cancellationToken);
        if (managerRole is null)
        {
            return ProjectErrors.ManagerRoleMissing;
        }

        var template = await LoadTemplateAsync(command.TemplateId, cancellationToken);
        if (template.IsFailure)
        {
            return template.Error;
        }

        var types = await ChooseTypesAsync(template.Value, cancellationToken);
        var created = Project.Create(
            code, command.Name, command.Description, command.OwnerId, workflow.InitialStatusId, command.StartDate, command.EndDate, types.Allowed, types.Default);
        if (created.IsFailure)
        {
            return created.Error; // R2, R12
        }

        var members = new List<(Guid UserId, Guid RoleId)> { (owner.Id, managerRole.Id) };
        if (caller.Value.UserId != owner.Id)
        {
            members.Add((caller.Value.UserId, managerRole.Id));
        }

        members.AddRange(await SuggestedMembersAsync(template.Value, members, cancellationToken));

        var withMembers = created.Value.SetMembers(members);
        if (withMembers.IsFailure)
        {
            return withMembers.Error;
        }

        db.Projects.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var member in members)
        {
            await access.InvalidateAsync(member.UserId, cancellationToken); // their project permissions just appeared
        }

        return created.Value.ToResponse();
    }

    private async Task<Result<ProjectTemplate?>> LoadTemplateAsync(Guid? templateId, CancellationToken cancellationToken)
    {
        if (templateId is not { } id)
        {
            return Result.Success<ProjectTemplate?>(null);
        }

        var template = await db.ProjectTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return Result.Failure<ProjectTemplate?>(ProjectErrors.TemplateNotFound);
        }

        return template.IsActive ? Result.Success<ProjectTemplate?>(template) : Result.Failure<ProjectTemplate?>(ProjectErrors.TemplateInactive); // R5
    }

    /// <summary>R5: the template types, or every active type with <c>task</c> as the default.</summary>
    private async Task<(List<Guid> Allowed, Guid Default)> ChooseTypesAsync(ProjectTemplate? template, CancellationToken cancellationToken)
    {
        if (template is not null)
        {
            return ([.. template.AllowedTypeIds], template.DefaultTypeId);
        }

        var active = await db.WorkItemTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).Select(t => new { t.Id, t.Key }).ToListAsync(cancellationToken);
        var preferred = active.Find(t => t.Key == DefaultTypeKey)?.Id ?? active.FirstOrDefault()?.Id ?? Guid.Empty;
        return ([.. active.Select(t => t.Id)], preferred);
    }

    /// <summary>R5: template members with a user are added when the user is active and the role is a project role; others are skipped.</summary>
    private async Task<List<(Guid UserId, Guid RoleId)>> SuggestedMembersAsync(
        ProjectTemplate? template, List<(Guid UserId, Guid RoleId)> already, CancellationToken cancellationToken)
    {
        var suggested = template?.SuggestedMembers.Where(s => s.UserId is not null).ToList() ?? [];
        if (suggested.Count == 0)
        {
            return [];
        }

        var userIds = suggested.Select(s => s.UserId!.Value).Distinct().ToList();
        var roleIds = suggested.Select(s => s.RoleId).Distinct().ToList();
        var activeUsers = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken);
        var projectRoles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Project).Select(r => r.Id).ToListAsync(cancellationToken);

        return [.. suggested
            .Where(s => activeUsers.Contains(s.UserId!.Value) && projectRoles.Contains(s.RoleId) && already.TrueForAll(a => a.UserId != s.UserId))
            .GroupBy(s => s.UserId!.Value)
            .Select(g => (g.Key, g.First().RoleId))];
    }
}
