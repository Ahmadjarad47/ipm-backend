using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ipm.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores an enum as the same kebab-case text the API uses (<c>InProduction</c> → <c>in-production</c>),
/// so the database, the JSON and the front end's string unions all read the same.
/// </summary>
public sealed class KebabCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToText(value),
    text => FromText(text))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Texts =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => JsonNamingPolicy.KebabCaseLower.ConvertName(v.ToString()));

    private static readonly Dictionary<string, TEnum> Values = Texts.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    /// <summary>The stored texts, for CHECK constraints.</summary>
    public static IEnumerable<string> AllTexts => Texts.Values;

    private static string ToText(TEnum value) => Texts[value];

    private static TEnum FromText(string text) => Values[text];
}
