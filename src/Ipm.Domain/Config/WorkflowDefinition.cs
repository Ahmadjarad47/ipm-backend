using Ipm.Domain.Identity;

namespace Ipm.Domain.Config;

/// <summary>
/// The structural rules of a workflow (R6 to R10), in one place: the validator reports every problem with its exact path
/// (<c>transitions[2].toStatusId</c>) and the entity refuses the definition again as a last line of defense.
/// Rules that need the database (the statuses exist, have this scope, are active) are checked by the handler.
/// </summary>
public static class WorkflowDefinition
{
    /// <summary>Every problem as (path, Arabic message). Empty means the definition is structurally valid.</summary>
    public static IReadOnlyList<(string Path, string Message)> Validate(
        StatusScope scope,
        IReadOnlyList<Guid> statusIds,
        Guid initialStatusId,
        IReadOnlyList<WorkflowTransition> transitions)
    {
        var problems = new List<(string, string)>();

        if (statusIds.Count == 0)
        {
            problems.Add(("statusIds", "يلزم حالة واحدة على الأقل."));
        }
        else if (statusIds.Distinct().Count() != statusIds.Count)
        {
            problems.Add(("statusIds", "توجد حالات مكررة."));
        }

        if (!statusIds.Contains(initialStatusId))
        {
            problems.Add(("initialStatusId", "الحالة الابتدائية يجب أن تكون من حالات سير العمل."));
        }

        var seen = new HashSet<(Guid, Guid)>();
        for (var i = 0; i < transitions.Count; i++)
        {
            CheckTransition(transitions[i], $"transitions[{i}]", scope, statusIds, seen, problems);
        }

        return problems;
    }

    private static void CheckTransition(
        WorkflowTransition t, string at, StatusScope scope, IReadOnlyList<Guid> statusIds, HashSet<(Guid, Guid)> seen, List<(string, string)> problems)
    {
        if (!statusIds.Contains(t.FromStatusId))
        {
            problems.Add(($"{at}.fromStatusId", "الحالة المصدر ليست من حالات سير العمل."));
        }

        if (!statusIds.Contains(t.ToStatusId))
        {
            problems.Add(($"{at}.toStatusId", "الحالة الهدف ليست من حالات سير العمل."));
        }

        if (t.FromStatusId == t.ToStatusId)
        {
            problems.Add(($"{at}.toStatusId", "لا يُسمح بانتقال من الحالة إلى نفسها."));
        }
        else if (!seen.Add((t.FromStatusId, t.ToStatusId)))
        {
            problems.Add((at, "انتقال مكرر."));
        }

        if (!string.IsNullOrEmpty(t.Permission) && !Permissions.IsKnown(t.Permission))
        {
            problems.Add(($"{at}.permission", $"الصلاحية «{t.Permission}» غير معروفة."));
        }

        // A project has no assignee and no dependencies, so these two conditions mean nothing there.
        if (scope == StatusScope.Project && t.RequiresAssignee)
        {
            problems.Add(($"{at}.requiresAssignee", "شرط المسؤول يخص الأعمال فقط."));
        }

        if (scope == StatusScope.Project && t.BlockedByOpenDependencies)
        {
            problems.Add(($"{at}.blockedByOpenDependencies", "شرط الاعتماديات المفتوحة يخص الأعمال فقط."));
        }
    }
}
