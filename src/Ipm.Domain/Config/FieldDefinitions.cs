using System.Text.RegularExpressions;

namespace Ipm.Domain.Config;

/// <summary>
/// The rules of R3 for a list of custom fields, in one place: the validator reports every problem with its exact path
/// (<c>fields[1].options</c>) and the entity refuses the list again as a last line of defense.
/// </summary>
public static partial class FieldDefinitions
{
    public const int MaxFields = 40;
    public const int LabelMaxLength = 100;
    public const int HintMaxLength = 200;

    /// <summary>Every problem as (path, Arabic message). Empty means the list is valid.</summary>
    public static IReadOnlyList<(string Path, string Message)> Validate(IReadOnlyList<FieldDefinition> fields)
    {
        var problems = new List<(string, string)>();
        if (fields.Count > MaxFields)
        {
            problems.Add(("fields", $"الحد الأقصى {MaxFields} حقلاً لكل نوع."));
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < fields.Count; i++)
        {
            Check(fields[i], $"fields[{i}]", keys, problems);
        }

        return problems;
    }

    private static void Check(FieldDefinition field, string at, HashSet<string> keys, List<(string, string)> problems)
    {
        if (string.IsNullOrEmpty(field.Key) || !KeyPattern().IsMatch(field.Key))
        {
            problems.Add(($"{at}.key", "مفتاح الحقل: أحرف إنجليزية صغيرة وأرقام و_ ويبدأ بحرف (حتى 41 خانة)."));
        }
        else if (!keys.Add(field.Key))
        {
            problems.Add(($"{at}.key", $"مفتاح الحقل «{field.Key}» مكرر."));
        }

        if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > LabelMaxLength)
        {
            problems.Add(($"{at}.label", $"اسم الحقل مطلوب ولا يزيد على {LabelMaxLength} حرفاً."));
        }

        if (!Enum.IsDefined(field.Type))
        {
            problems.Add(($"{at}.type", "نوع الحقل غير مسموح."));
        }

        if (field.Hint is { Length: > HintMaxLength })
        {
            problems.Add(($"{at}.hint", $"النص الإرشادي لا يزيد على {HintMaxLength} حرفاً."));
        }

        if (field.Order < 0)
        {
            problems.Add(($"{at}.order", "ترتيب العرض عدد صحيح غير سالب."));
        }

        if (field.Type == FieldType.Select)
        {
            CheckOptions(field.Options ?? [], at, problems);
        }

        if (field.Min is { } min && field.Max is { } max && min > max)
        {
            problems.Add(($"{at}.max", "الحد الأعلى يجب ألا يقل عن الحد الأدنى."));
        }
    }

    private static void CheckOptions(IReadOnlyList<FieldOption> options, string at, List<(string, string)> problems)
    {
        if (options.Count == 0)
        {
            problems.Add(($"{at}.options", "حقل الاختيار يتطلب خياراً واحداً على الأقل."));
            return;
        }

        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Value) || string.IsNullOrWhiteSpace(option.Label))
            {
                problems.Add(($"{at}.options", "قيمة الخيار واسمه مطلوبان."));
            }
            else if (!values.Add(option.Value))
            {
                problems.Add(($"{at}.options", $"قيمة الخيار «{option.Value}» مكررة."));
            }
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,40}$")]
    private static partial Regex KeyPattern();
}
