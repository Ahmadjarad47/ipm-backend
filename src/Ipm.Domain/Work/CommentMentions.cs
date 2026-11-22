using System.Text.RegularExpressions;

namespace Ipm.Domain.Work;

/// <summary>R13: finds @username in a comment. A mention starts the text or follows a space, and the name ends before any letter, digit, dot or dash.</summary>
public static class CommentMentions
{
    public static IReadOnlyList<Guid> Extract(string text, IEnumerable<(Guid Id, string Username)> candidates) =>
    [
        .. candidates
            .Where(c => !string.IsNullOrEmpty(c.Username)
                && Regex.IsMatch(
                    text, $@"(^|\s)@{Regex.Escape(c.Username)}(?![\w.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
            .Select(c => c.Id),
    ];
}
