using System.Globalization;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>The proposed values of a sensitive change, read as the types of the fields (06b R4). Every value was checked to parse before this is used.</summary>
internal sealed class ProposedValues(IReadOnlyDictionary<string, string?> raw)
{
    public IReadOnlyDictionary<string, string?> Raw => raw;

    public bool Has(string field) => raw.ContainsKey(field);

    public string? Text(string field) => raw.GetValueOrDefault(field);

    public DateOnly? Date(string field) => TryDate(raw.GetValueOrDefault(field), out var date) ? date : null;

    public Guid? Id(string field) => Guid.TryParse(raw.GetValueOrDefault(field), out var id) ? id : null;

    public IReadOnlyList<Guid> Ids(string field) => ParseIds(raw.GetValueOrDefault(field)) ?? [];

    public Priority PriorityOr(Priority current) => Has("priority") && Enum.TryParse<Priority>(raw["priority"], ignoreCase: true, out var p) ? p : current;

    public static bool TryDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>The ids of a comma separated list, or null when one of them is not a GUID. An empty text is the empty list.</summary>
    public static List<Guid>? ParseIds(string? text)
    {
        var ids = new List<Guid>();
        foreach (var part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
            {
                return null;
            }

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>The text form the snapshot uses, so a proposed value and the current one compare directly (06b R5).</summary>
    public static string? Normalize(string field, string? value) => field switch
    {
        "assigneeIds" => ParseIds(value) is { } ids ? string.Join(',', ids.Select(i => i.ToString()).Order(StringComparer.Ordinal)) : value,
        "parentId" or "typeId" or "ownerId" => Guid.TryParse(value, out var id) ? id.ToString() : (string.IsNullOrWhiteSpace(value) ? null : value),
        "priority" => value?.Trim().ToLowerInvariant(),
        "title" or "name" => value?.Trim(),
        "description" => value?.Trim() ?? string.Empty,
        _ => string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
    };
}
