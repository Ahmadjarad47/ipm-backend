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

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectMembers;

internal sealed class SetProjectMembersCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<SetProjectMembersCommand, ProjectMembersResponse>
{
    public async ValueTask<Result<ProjectMembersResponse>> Handle(SetProjectMembersCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.ProjectsMembersManage, cancellationToken);
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

        var before = project.Members.ToDictionary(m => m.UserId, m => m.RoleId);
        var wanted = command.Members.Select(m => (m.UserId, m.RoleId)).ToList();
        var added = wanted.Where(m => !before.ContainsKey(m.UserId)).ToList();
        var changed = wanted.Where(m => before.TryGetValue(m.UserId, out var role) && role != m.RoleId).ToList();
        var removed = before.Where(b => wanted.TrueForAll(w => w.UserId != b.Key)).Select(b => (UserId: b.Key, RoleId: b.Value)).ToList();

        var valid = await EnsureNewMembersValidAsync([.. added, .. changed], cancellationToken);
        if (valid.IsFailure)
        {
            return valid.Error; // R7
        }

        // R8: handing out, taking away or lowering a role needs every permission of that role inside this project.
        var roleIds = added.Concat(changed).Select(m => m.RoleId).Concat(removed.Select(m => m.RoleId)).Concat(changed.Select(m => before[m.UserId])).Distinct().ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        var touched = added.Select(m => m.RoleId).Concat(changed.Select(m => m.RoleId)).Concat(removed.Select(m => m.RoleId)).Concat(changed.Select(m => before[m.UserId]));
        foreach (var roleId in touched.Distinct())
        {
            var grantable = ProjectGuard.EnsureGrantableIn(caller, project.Id, roles.TryGetValue(roleId, out var role) ? role.EffectivePermissions : []);
            if (grantable.IsFailure)
            {
                return grantable.Error;
            }
        }

        var set = project.SetMembers(wanted); // R7: unique users, the owner included
        if (set.IsFailure)
        {
            return set.Error;
        }

        if (added.Count + changed.Count + removed.Count > 0)
        {
            await ApprovalSupersession.SupersedeAsync(db, ApprovalSubjectType.Project, project.Id, ["members"], cancellationToken); // R11 of part 06
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var userId in added.Concat(changed).Concat(removed).Select(m => m.UserId).Distinct())
        {
            await access.InvalidateAsync(userId, cancellationToken); // their permissions in this project changed
        }

        // R17 (part 04, filled from part 05): open work still assigned to a removed member is left as it is, with a warning.
        return new ProjectMembersResponse(project.ToResponse(), await WarningsAsync(project.Id, removed.Select(m => m.UserId).ToList(), cancellationToken));
    }

    private async Task<IReadOnlyList<MemberWarning>> WarningsAsync(Guid projectId, List<Guid> removedUserIds, CancellationToken cancellationToken)
    {
        if (removedUserIds.Count == 0)
        {
            return [];
        }

        var open = await (
                from i in db.WorkItems.AsNoTracking()
                join s in db.Statuses on i.StatusId equals s.Id
                where i.ProjectId == projectId && !i.IsArchived && s.Category != StatusCategory.Done && s.Category != StatusCategory.Cancelled
                orderby i.Number
                select new { i.Ref, i.AssigneeIds })
            .ToListAsync(cancellationToken);
        var names = await db.Users.AsNoTracking().Where(u => removedUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var warnings = new List<MemberWarning>();
        foreach (var userId in removedUserIds)
        {
            var refs = open.Where(i => i.AssigneeIds.Contains(userId)).Select(i => i.Ref).ToList();
            if (refs.Count > 0)
            {
                var name = names.GetValueOrDefault(userId, "—");
                warnings.Add(new MemberWarning(userId, name, refs, $"«{name}» ما زال مكلّفاً بـ {refs.Count} عمل مفتوح. لم يتغيّر شيء عليها، وأعد تكليفها عند اللزوم."));
            }
        }

        return warnings;
    }

    /// <summary>R7: every added or re-roled member is an active user and has a project-scope role.</summary>
    private async Task<Result> EnsureNewMembersValidAsync(List<(Guid UserId, Guid RoleId)> members, CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return Result.Success();
        }

        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var roleIds = members.Select(m => m.RoleId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);

        foreach (var (userId, roleId) in members)
        {
            if (!users.TryGetValue(userId, out var user))
            {
                return ProjectErrors.MembersInvalid("أحد المستخدمين المختارين غير موجود.");
            }

            if (!user.IsActive)
            {
                return ProjectErrors.MembersInvalid($"المستخدم «{user.Name}» غير نشط.");
            }

            if (!roles.TryGetValue(roleId, out var role))
            {
                return ProjectErrors.MembersInvalid("أحد الأدوار المختارة غير موجود.");
            }

            if (role.Scope != RoleScope.Project)
            {
                return ProjectErrors.MembersInvalid($"الدور «{role.Name}» ليس دور مشروع.");
            }
        }

        return Result.Success();
    }
}
