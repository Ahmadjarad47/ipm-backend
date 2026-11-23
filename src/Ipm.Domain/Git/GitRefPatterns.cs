using System.Text.RegularExpressions;

namespace Ipm.Domain.Git;

/// <summary>
/// R4 and R7: the pattern that finds work item refs (<c>ERP-104</c>) in commit messages, branches and PR titles. It runs on the
/// non-backtracking engine with a timeout, so a hostile pattern cannot cause catastrophic backtracking; back-references and
/// lookarounds, which that engine cannot run, are refused. Only the first <see cref="MaxScanChars"/> characters of a text are read.
/// </summary>
public static class GitRefPatterns
{
    public const int MaxLength = 100;
    public const int MaxScanChars = 2000;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    /// <summary>The default pattern of a project: its code and a number, as a whole word.</summary>
    public static string Default(string projectCode) => $@"\b({Regex.Escape(projectCode)}-\d+)\b";

    private static Regex Compile(string pattern) =>
        new(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, Timeout);

    /// <summary>An Arabic reason, or null when the pattern is acceptable. It must recognise the project own refs (<paramref name="sampleRefs"/>).</summary>
    public static string? Validate(string pattern, IReadOnlyCollection<string> sampleRefs)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "النمط فارغ.";
        }

        if (pattern.Length > MaxLength)
        {
            return $"النمط أطول من {MaxLength} حرفاً.";
        }

        Regex regex;
        try
        {
            regex = Compile(pattern);
        }
        catch (ArgumentException)
        {
            return "النمط ليس تعبيراً نمطياً صالحاً أو يستعمل مراجع خلفية أو تطلّعاً أمامياً/خلفياً (مرفوض).";
        }
        catch (NotSupportedException)
        {
            return "النمط ليس تعبيراً نمطياً صالحاً أو يستعمل مراجع خلفية أو تطلّعاً أمامياً/خلفياً (مرفوض).";
        }

        if (regex.GetGroupNumbers().Length - 1 != 1)
        {
            return "يجب أن يحتوي النمط على مجموعة التقاط واحدة بالضبط تلتقط رمز العنصر.";
        }

        foreach (var reference in sampleRefs)
        {
            string[] forms = [reference, $"[{reference}]", $"({reference})", $"#{reference}", $"feature/{reference}", $"{reference}: وصف"];
            if (!forms.Any(text => Matches(regex, text).Contains(reference)))
            {
                return $"النمط لا يلتقط رمز عنصر المشروع «{reference}».";
            }
        }

        return null;
    }

    /// <summary>The refs found in the texts by the pattern (falling back to the default pattern when the stored one cannot run).</summary>
    public static IReadOnlyList<string> Extract(string pattern, string projectCode, params string?[] texts)
    {
        Regex regex;
        try
        {
            regex = Compile(pattern);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            regex = Compile(Default(projectCode)); // a stored pattern that no longer compiles must not stop ingestion
        }

        var found = new List<string>();
        foreach (var text in texts.Where(t => !string.IsNullOrEmpty(t)))
        {
            foreach (var reference in Matches(regex, text!))
            {
                if (!found.Contains(reference))
                {
                    found.Add(reference);
                }
            }
        }

        return found;
    }

    private static List<string> Matches(Regex regex, string text)
    {
        try
        {
            var scanned = text.Length > MaxScanChars ? text[..MaxScanChars] : text;
            return [.. regex.Matches(scanned).Select(m => m.Groups[1].Value).Where(v => v.Length > 0)];
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }
}
