using System.Globalization;

namespace Ipm.Application.Abstractions.Git;

/// <summary>Reads a time as Gitea, GitHub and GitLab write it (ISO 8601, or GitLab's older <c>2024-01-02 10:00:00 UTC</c>).</summary>
public static class GitTime
{
    public static DateTimeOffset? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.EndsWith(" UTC", StringComparison.Ordinal))
        {
            value = value[..^4] + "Z";
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            && parsed.Year > 1970
            ? parsed
            : null;
    }
}
