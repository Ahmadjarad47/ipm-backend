using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Converters;

/// <summary>
/// Maps a list of plain records to a <c>jsonb</c> column, with the same JSON the API speaks (camelCase, enums as kebab-case text).
/// The comparer looks at the serialized JSON, so EF Core notices a changed element and the row version (xmin) moves.
/// </summary>
public static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static PropertyBuilder<List<T>> HasJsonbList<T>(this PropertyBuilder<List<T>> property)
    {
        property.HasColumnType("jsonb")
            .HasConversion(
                list => JsonSerializer.Serialize(list, Options),
                json => Deserialize<T>(json),
                new ValueComparer<List<T>>(
                    (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                    list => JsonSerializer.Serialize(list, Options).GetHashCode(StringComparison.Ordinal),
                    list => Deserialize<T>(JsonSerializer.Serialize(list, Options))));
        return property;
    }

    private static List<T> Deserialize<T>(string json) => JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
}
