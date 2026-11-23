using Ipm.Application.Common.Exceptions;

namespace Ipm.Api.Infrastructure;

/// <summary>Query parameters that take several values separated by commas (<c>statusId=a,b</c>): any of them matches.</summary>
public static class ListParameter
{
    /// <summary>Ids; an empty or missing parameter means no filter, a value that is not an id is a 400 naming the parameter.</summary>
    public static IReadOnlyList<Guid> Guids(string? value, string name)
    {
        var ids = new List<Guid>();
        foreach (var part in Split(value))
        {
            if (!Guid.TryParse(part, out var id))
            {
                throw new ValidationException(new Dictionary<string, string[]> { [name] = [$"قيمة غير صالحة: {part}"] });
            }

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>Plain words (e.g. priorities); validated by the query's own validator.</summary>
    public static IReadOnlyList<string> Words(string? value) => Split(value);

    private static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
}
