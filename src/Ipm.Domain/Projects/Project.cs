using System.Text.RegularExpressions;
using Ipm.Domain.Common;

namespace Ipm.Domain.Projects;

/// <summary>
/// A project: owner, members and their roles, allowed work item types, and a status that moves only through the project
/// workflow (R9). Never deleted, only archived; an archived project is read-only (R6). The <see cref="Code"/> never changes (R1).
/// Members are rows of their own, so every member change moves <see cref="MembersRevision"/> and with it the project ETag.
/// </summary>
public sealed partial class Project : AuditableEntity
{
    public const int NameMaxLength = 200;
    public const int CodeMaxLength = 10;

    private readonly List<ProjectMember> _members = [];

    private Project() { }

    private Project(Guid id) : base(id) { }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    public Guid StatusId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public bool IsArchived { get; private set; }

    public List<Guid> AllowedTypeIds { get; private set; } = [];

    public Guid DefaultTypeId { get; private set; }

    public List<WorkflowOverride> WorkflowOverrides { get; private set; } = [];

    /// <summary>Counts member changes, so the project version (ETag) moves even though the members live in their own table.</summary>
    public int MembersRevision { get; private set; }

    public IReadOnlyCollection<ProjectMember> Members => _members;

    /// <summary>Codes are compared and stored in upper case, so <c>erp</c> and <c>ERP</c> are the same code.</summary>
    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public static bool IsValidCode(string normalizedCode) => CodePattern().IsMatch(normalizedCode);

    /// <summary>
    /// A project with no members yet; add them with <see cref="SetMembers"/> (R3 needs the owner among them).
    /// <paramref name="id"/> is only given by the seed, to keep it idempotent.
    /// </summary>
    public static Result<Project> Create(
        string code, string name, string description, Guid ownerId, Guid statusId, DateOnly startDate, DateOnly endDate,
        IEnumerable<Guid> allowedTypeIds, Guid defaultTypeId, Guid? id = null)
    {
        var normalized = NormalizeCode(code);
        if (!IsValidCode(normalized))
        {
            return ProjectErrors.CodeInvalid;
        }

        var project = id is { } fixedId ? new Project(fixedId) : new Project();
        project.Code = normalized;
        project.OwnerId = ownerId;
        project.StatusId = statusId;

        var updated = project.Update(name, description, startDate, endDate);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        var types = project.SetAllowedTypes(allowedTypeIds, defaultTypeId);
        if (types.IsFailure)
        {
            return types.Error;
        }

        return project;
    }

    /// <summary>R2: the plain fields. The end date is not before the start date.</summary>
    public Result Update(string name, string description, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            return ProjectErrors.InvalidDates;
        }

        Name = name.Trim();
        Description = description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        return Result.Success();
    }

    /// <summary>R1: a request may repeat the code, but never a different one.</summary>
    public Result EnsureCodeUnchanged(string? requestedCode) =>
        requestedCode is null || NormalizeCode(requestedCode) == Code ? Result.Success() : ProjectErrors.CodeImmutable;

    /// <summary>R6: an archived project is read-only.</summary>
    public Result EnsureEditable() => IsArchived ? ProjectErrors.Archived : Result.Success();

    /// <summary>R3: the new owner is already a member (the handler checks that the user exists and is active).</summary>
    public Result ChangeOwner(Guid newOwnerId)
    {
        if (newOwnerId == OwnerId)
        {
            return Result.Success();
        }

        if (!_members.Exists(m => m.UserId == newOwnerId))
        {
            return ProjectErrors.OwnerMustBeMember;
        }

        OwnerId = newOwnerId;
        return Result.Success();
    }

    public void Archive() => IsArchived = true;

    public void Unarchive() => IsArchived = false;

    /// <summary>R9: the only way the status changes. The workflow rules are checked by the handler.</summary>
    public void MoveTo(Guid statusId) => StatusId = statusId;

    /// <summary>
    /// R7: replaces the whole member list: one entry per user, the owner included. Existing members keep their row when only the
    /// role changes; the revision moves when anything changed. The users and roles themselves are checked by the handler.
    /// </summary>
    public Result SetMembers(IReadOnlyCollection<(Guid UserId, Guid RoleId)> members)
    {
        if (members.Select(m => m.UserId).Distinct().Count() != members.Count)
        {
            return ProjectErrors.MembersInvalid("يوجد مستخدم مكرر في قائمة الأعضاء.");
        }

        if (!members.Any(m => m.UserId == OwnerId))
        {
            return ProjectErrors.OwnerMustBeMember;
        }

        var changed = false;
        foreach (var gone in _members.Where(m => members.All(w => w.UserId != m.UserId)).ToList())
        {
            _members.Remove(gone);
            changed = true;
        }

        foreach (var (userId, roleId) in members)
        {
            var existing = _members.Find(m => m.UserId == userId);
            if (existing is null)
            {
                _members.Add(new ProjectMember(Id, userId, roleId));
                changed = true;
            }
            else if (existing.RoleId != roleId)
            {
                existing.ChangeRole(roleId);
                changed = true;
            }
        }

        if (changed)
        {
            MembersRevision++;
        }

        return Result.Success();
    }

    /// <summary>R12: at least one type, and the default is one of them. The types themselves are checked by the handler.</summary>
    public Result SetAllowedTypes(IEnumerable<Guid> allowedTypeIds, Guid defaultTypeId)
    {
        var allowed = allowedTypeIds.Distinct().ToList();
        if (allowed.Count == 0 || !allowed.Contains(defaultTypeId))
        {
            return ProjectErrors.TypesInvalid;
        }

        AllowedTypeIds = allowed;
        DefaultTypeId = defaultTypeId;
        return Result.Success();
    }

    /// <summary>R13: replaces the workflow overrides, one per type. The types and workflows are checked by the handler.</summary>
    public void SetWorkflowOverrides(IEnumerable<WorkflowOverride> overrides) =>
        WorkflowOverrides = [.. overrides.GroupBy(o => o.TypeId).Select(g => g.Last())];

    /// <summary>The workflow work items of this type follow in this project: the override if there is one, else the type own.</summary>
    public Guid EffectiveWorkflowId(Guid typeId, Guid typeWorkflowId) =>
        WorkflowOverrides.Find(o => o.TypeId == typeId)?.WorkflowId ?? typeWorkflowId;

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    private static partial Regex CodePattern();
}
