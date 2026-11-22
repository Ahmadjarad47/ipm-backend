using System.Globalization;
using System.Text.Json;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R2 and R3: which template governs a subject and an action.</summary>
internal static class TemplateMatcher
{
    /// <summary>Does <paramref name="template"/> govern this subject? <paramref name="action"/> narrows by the event and by the rule actions.</summary>
    public static bool Matches(ApprovalTemplate template, ApprovalSubject subject, ApprovalAction? action = null)
    {
        if (ApprovalSubjectFields.SubjectOf(template.Event, template.ProtectedFields) != subject.Type)
        {
            return false;
        }

        if (action is { } wanted && template.Event != ApprovalSubjectFields.EventFor(subject.Type, wanted))
        {
            return false;
        }

        var rule = template.Rule;
        if (rule is null)
        {
            return true;
        }

        if (rule.ProjectIds is { Count: > 0 } projects && !projects.Contains(subject.ProjectId))
        {
            return false;
        }

        if (rule.WorkItemTypeIds is { Count: > 0 } types && (subject.Item is null || !types.Contains(subject.Item.TypeId)))
        {
            return false;
        }

        if (rule.Priorities is { Count: > 0 } priorities && (subject.Item is null || !priorities.Contains(subject.Item.Priority)))
        {
            return false;
        }

        if (action is { } a && rule.Actions is { Count: > 0 } actions && !actions.Contains(a))
        {
            return false;
        }

        return (rule.Conditions ?? []).All(c => Holds(c, ApprovalSubjects.ConditionValue(subject, c.Field)));
    }

    /// <summary>R3: the active template with the highest rule priority, then the most specific rule, then the oldest.</summary>
    public static async Task<ApprovalTemplate?> BestAsync(IApplicationDbContext db, ApprovalSubject subject, ApprovalAction action, CancellationToken cancellationToken)
    {
        var templates = await db.ApprovalTemplates.AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);
        return templates
            .Where(t => Matches(t, subject, action))
            .OrderByDescending(t => t.Rule?.Priority ?? 0)
            .ThenByDescending(t => Specificity(t.Rule))
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .FirstOrDefault();
    }

    private static int Specificity(ApprovalRule? rule) => rule is null
        ? 0
        : new[] { rule.WorkItemTypeIds?.Count, rule.ProjectIds?.Count, rule.Priorities?.Count, rule.Actions?.Count, rule.Conditions?.Count }.Count(c => c > 0);

    private static bool Holds(RuleCondition condition, string? actual)
    {
        var value = condition.Value;
        switch (condition.Op)
        {
            case "eq": return (actual ?? string.Empty) == Text(value);
            case "neq": return (actual ?? string.Empty) != Text(value);
            case "in": return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(v => Text(v) == (actual ?? string.Empty));
            default:
                var both = decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var left) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var right);
                var order = both
                    ? decimal.Compare(left, value.GetDecimal())
                    : string.CompareOrdinal(actual ?? string.Empty, Text(value));
                return condition.Op == "gt" ? order > 0 : order < 0;
        }
    }

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
}
