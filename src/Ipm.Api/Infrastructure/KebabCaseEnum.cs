using System.Text.Json;

namespace Ipm.Api.Infrastructure;

/// <summary>
/// Reads an enum from the query string in the same kebab-case the JSON uses (<c>?status=on-hold</c>).
/// Minimal APIs bind query enums with Enum.TryParse, which knows nothing about kebab-case.
/// </summary>
public static class KebabCaseEnum
{
    /// <summary>
    /// Null text gives a null value. Unknown text gives a 400 problem in <paramref name="problem"/>
    /// listing the allowed values.
    /// </summary>
    public static TEnum? ParseQuery<TEnum>(string? text, string parameter, out IResult? problem)
        where TEnum : struct, Enum
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Text(value), text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        problem = Results.Problem(
            detail: $"قيمة {parameter} غير صالحة. القيم المسموحة: {string.Join("، ", Enum.GetValues<TEnum>().Select(Text))}.",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["code"] = "Request.InvalidEnum" });
        return null;
    }

    private static string Text<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}
