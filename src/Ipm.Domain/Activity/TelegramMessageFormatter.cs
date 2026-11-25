namespace Ipm.Domain.Activity;

/// <summary>Spec 11 R7: the text of a Telegram message. Everything that came from a user is HTML-escaped, the rest is fixed markup.</summary>
public static class TelegramMessageFormatter
{
    /// <summary>The absolute link into the app, or null when no base address is set.</summary>
    public static string? AbsoluteUrl(string? appBaseUrl, string link) =>
        string.IsNullOrWhiteSpace(appBaseUrl) ? null : appBaseUrl.TrimEnd('/') + (link.StartsWith('/') ? link : "/" + link);

    public static string Format(string title, string body, string? url)
    {
        var text = $"<b>{Escape(title)}</b>\n{Escape(body)}";
        return url is null ? text : $"{text}\n{Escape(url)}";
    }

    /// <summary>Telegram HTML mode needs exactly these three escaped.</summary>
    public static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>R7: an inline URL button is only accepted by Telegram for public https addresses (localhost is rejected).</summary>
    public static bool CanUseButton(string? url) => url is not null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
