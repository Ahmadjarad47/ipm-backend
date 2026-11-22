using System.Text.Json;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common.Exceptions;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>The checks that need the database and are shared by create, update and the bulk actions (R4, R5).</summary>
internal static class WorkItemChecks
{
    /// <summary>
    /// R4: no duplicates, and every newly added assignee is an active member of the project. An assignee who is already on the item
    /// stays even if they have left the project since, until the list is changed.
    /// </summary>
    public static async Task<Result> EnsureAssigneesAsync(
        IApplicationDbContext db, Project project, IReadOnlyCollection<Guid> wanted, IReadOnlyCollection<Guid> current, CancellationToken cancellationToken)
    {
        if (wanted.Distinct().Count() != wanted.Count)
        {
            return WorkItemErrors.AssigneesInvalid;
        }

        var added = wanted.Where(id => !current.Contains(id)).ToList();
        if (added.Count == 0)
        {
            return Result.Success();
        }

        if (added.Exists(id => project.Members.All(m => m.UserId != id)))
        {
            return WorkItemErrors.AssigneesInvalid;
        }

        var active = await db.Users.AsNoTracking().CountAsync(u => added.Contains(u.Id) && u.IsActive, cancellationToken);
        return active == added.Count ? Result.Success() : WorkItemErrors.AssigneesInvalid;
    }

    /// <summary>
    /// R5: the values against the type field definitions. Throws a validation error with the exact path of every problem
    /// (<c>fields.severity</c>), like the validators do. Returns the normalized values.
    /// </summary>
    public static async Task<Dictionary<string, JsonElement>> EnsureFieldsAsync(
        IApplicationDbContext db, WorkItemType type, IReadOnlyDictionary<string, JsonElement>? values, IReadOnlyDictionary<string, JsonElement>? existing,
        CancellationToken cancellationToken)
    {
        var normalized = WorkItemFieldValues.Normalize(values);
        var problems = WorkItemFieldValues.Validate(type.Fields, normalized, existing).ToList();

        var userIds = WorkItemFieldValues.UserIds(type.Fields, normalized);
        if (userIds.Count > 0)
        {
            var found = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken);
            foreach (var definition in type.Fields.Where(d => d.Type == FieldType.User && normalized.ContainsKey(d.Key)))
            {
                if (Guid.TryParse(normalized[definition.Key].GetString(), out var id) && !found.Contains(id))
                {
                    problems.Add(($"fields.{definition.Key}", $"المستخدم المختار في الحقل «{definition.Label}» غير موجود."));
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new ValidationException(problems.GroupBy(p => p.Path, p => p.Message).ToDictionary(g => g.Key, g => g.Distinct().ToArray()));
        }

        return normalized;
    }
}
