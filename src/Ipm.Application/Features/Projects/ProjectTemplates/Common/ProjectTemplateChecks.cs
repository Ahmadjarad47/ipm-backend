using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Common;

/// <summary>Checks that need the database, shared by the create and update commands of a template (R14, R15).</summary>
internal static class ProjectTemplateChecks
{
    private const string ProjectWorkflowKey = "project_default";

    /// <summary>R15: template names are unique regardless of letter case. <paramref name="exceptId"/> is the template being edited.</summary>
    public static async Task<bool> NameTakenAsync(IApplicationDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var names = await db.ProjectTemplates.AsNoTracking().Where(t => t.Id != exceptId).Select(t => t.Name).ToListAsync(cancellationToken);
        return names.Any(n => string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// R14: the types, roles and members the template names exist, and the initial status is the initial status of the project workflow
    /// (an omitted one means exactly that). Returns the initial status to store.
    /// </summary>
    public static async Task<Result<Guid>> ValidateAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> allowedTypeIds,
        Guid? initialStatusId,
        IReadOnlyCollection<Guid> suggestedRoleIds,
        IReadOnlyCollection<SuggestedMember> suggestedMembers,
        CancellationToken cancellationToken)
    {
        var workflow = await db.Workflows.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Key == ProjectWorkflowKey && w.Scope == StatusScope.Project, cancellationToken);
        if (workflow is null)
        {
            return ProjectErrors.WorkflowMissing;
        }

        if (initialStatusId is { } initial && initial != workflow.InitialStatusId)
        {
            return ProjectTemplateErrors.InvalidInitialStatus;
        }

        var typeIds = allowedTypeIds.Distinct().ToList();
        if (await db.WorkItemTypes.CountAsync(t => typeIds.Contains(t.Id), cancellationToken) != typeIds.Count)
        {
            return ProjectTemplateErrors.InvalidTypes;
        }

        var roleIds = suggestedRoleIds.Concat(suggestedMembers.Select(m => m.RoleId)).Distinct().ToList();
        if (await db.Roles.CountAsync(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Project, cancellationToken) != roleIds.Count)
        {
            return suggestedMembers.Count > 0 && suggestedRoleIds.Count == 0 ? ProjectTemplateErrors.InvalidMembers : ProjectTemplateErrors.InvalidRoles;
        }

        var userIds = suggestedMembers.Where(m => m.UserId is not null).Select(m => m.UserId!.Value).Distinct().ToList();
        if (await db.Users.CountAsync(u => userIds.Contains(u.Id), cancellationToken) != userIds.Count)
        {
            return ProjectTemplateErrors.InvalidMembers;
        }

        return workflow.InitialStatusId;
    }
}
