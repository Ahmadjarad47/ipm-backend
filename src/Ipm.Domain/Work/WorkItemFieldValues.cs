using System.Globalization;
using System.Text.Json;
using Ipm.Domain.Config;

namespace Ipm.Domain.Work;

/// <summary>
/// R5: the values of a work item custom fields against the field definitions of its type, in one place. It reports every problem
/// with its exact path (<c>fields.severity</c>); the entity refuses invalid values again as a last line of defense. A user value
/// is only checked for its shape here (a GUID); that the user exists is the handler job.
/// </summary>
public static class WorkItemFieldValues
{
    public const int TextMaxLength = 2000;

    /// <summary>Drops null values: a null (or an empty string) is the same as no value.</summary>
    public static Dictionary<string, JsonElement> Normalize(IReadOnlyDictionary<string, JsonElement>? values)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (values is null)
        {
            return result;
        }

        foreach (var (key, value) in values)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
            {
                continue;
            }

            result[key] = value.Clone();
        }

        return result;
    }

    /// <summary>
    /// Every problem as (path, Arabic message); empty means valid. <paramref name="values"/> must already be normalized.
    /// <paramref name="existing"/> are the values the item already holds: a key that is no longer a field of the type is kept if it was already there.
    /// </summary>
    public static IReadOnlyList<(string Path, string Message)> Validate(
        IReadOnlyList<FieldDefinition> definitions, IReadOnlyDictionary<string, JsonElement> values, IReadOnlyDictionary<string, JsonElement>? existing = null)
    {
        var problems = new List<(string, string)>();

        foreach (var definition in definitions.Where(d => d.Required && !values.ContainsKey(d.Key)))
        {
            problems.Add(($"fields.{definition.Key}", $"الحقل «{definition.Label}» مطلوب."));
        }

        foreach (var (key, value) in values)
        {
            var definition = definitions.FirstOrDefault(d => d.Key == key);
            if (definition is null)
            {
                if (existing is null || !existing.ContainsKey(key))
                {
                    problems.Add(($"fields.{key}", $"الحقل «{key}» غير معرّف لهذا النوع."));
                }

                continue;
            }

            if (Check(definition, value) is { } message)
            {
                problems.Add(($"fields.{key}", message));
            }
        }

        return problems;
    }

    private static string? Check(FieldDefinition field, JsonElement value) => field.Type switch
    {
        FieldType.Text => value.ValueKind != JsonValueKind.String ? Wrong(field, "نص")
            : value.GetString()!.Length > TextMaxLength ? $"الحقل «{field.Label}» لا يزيد على {TextMaxLength} حرفاً." : null,
        FieldType.Number => CheckNumber(field, value),
        FieldType.Date => value.ValueKind == JsonValueKind.String && IsDate(value.GetString()!) ? null : Wrong(field, "تاريخ بصيغة yyyy-MM-dd"),
        FieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : Wrong(field, "قيمة منطقية"),
        FieldType.Select => value.ValueKind == JsonValueKind.String && (field.Options ?? []).Any(o => o.Value == value.GetString())
            ? null : $"قيمة الحقل «{field.Label}» ليست من الخيارات المتاحة.",
        FieldType.User => value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out _) ? null : Wrong(field, "معرّف مستخدم"),
        _ => null,
    };

    private static string? CheckNumber(FieldDefinition field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
        {
            return Wrong(field, "رقم");
        }

        if (field.Min is { } min && number < min)
        {
            return $"قيمة الحقل «{field.Label}» لا تقل عن {min.ToString(CultureInfo.InvariantCulture)}.";
        }

        return field.Max is { } max && number > max ? $"قيمة الحقل «{field.Label}» لا تزيد على {max.ToString(CultureInfo.InvariantCulture)}." : null;
    }

    private static bool IsDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static string Wrong(FieldDefinition field, string expected) => $"قيمة الحقل «{field.Label}» يجب أن تكون {expected}.";

    /// <summary>True when both hold the same values (compared as JSON, in key order).</summary>
    public static bool Same(IReadOnlyDictionary<string, JsonElement> a, IReadOnlyDictionary<string, JsonElement> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var other) && p.Value.GetRawText() == other.GetRawText());

    /// <summary>The ids of the user fields' values, so the handler can check they exist.</summary>
    public static IReadOnlyList<Guid> UserIds(IReadOnlyList<FieldDefinition> definitions, IReadOnlyDictionary<string, JsonElement> values) =>
    [
        .. definitions
            .Where(d => d.Type == FieldType.User && values.TryGetValue(d.Key, out var v) && v.ValueKind == JsonValueKind.String)
            .Select(d => Guid.TryParse(values[d.Key].GetString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct(),
    ];
}
