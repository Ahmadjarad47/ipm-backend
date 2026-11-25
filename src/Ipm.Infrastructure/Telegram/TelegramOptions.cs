using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Ipm.Infrastructure.Telegram;

/// <summary>
/// Section <c>Telegram</c> (spec 11 R1). In production the token comes from <c>Telegram__BotToken</c>, never from a file in git.
/// Enabled without a token is the same as disabled, so a compose file without <c>.env</c> still starts.
/// </summary>
public sealed partial class TelegramOptions
{
    public const string Section = "Telegram";

    public bool Enabled { get; init; }

    public string BotToken { get; init; } = string.Empty;

    /// <summary>Without the at sign.</summary>
    public string BotUsername { get; init; } = string.Empty;

    public TelegramMode Mode { get; init; } = TelegramMode.Polling;

    /// <summary>Webhook mode: the public https address of <c>/api/v1/telegram/webhook</c>.</summary>
    public string WebhookUrl { get; init; } = string.Empty;

    /// <summary>Webhook mode: 1 to 256 of letters, digits, underscore and dash (Telegram rule for <c>secret_token</c>).</summary>
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>The address of the web app, to make absolute links in messages.</summary>
    public string AppBaseUrl { get; init; } = string.Empty;

    /// <summary>The cap for all chats together. Telegram allows about 30.</summary>
    [Range(1, 30)]
    public int MessagesPerSecond { get; init; } = 25;

    /// <summary>False keeps the dispatcher and the update loop from starting (integration tests call the dispatcher by hand).</summary>
    public bool BackgroundServices { get; init; } = true;

    /// <summary>The only condition under which the feature runs.</summary>
    public bool IsActive => Enabled && !string.IsNullOrWhiteSpace(BotToken);

    /// <summary>Null when fine; otherwise why an active configuration cannot work.</summary>
    public string? Problem()
    {
        if (!IsActive)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(BotUsername))
        {
            return "Telegram:BotUsername is required when Telegram is enabled.";
        }

        if (Mode == TelegramMode.Webhook)
        {
            if (!Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            {
                return "Telegram:WebhookUrl must be an https address in Webhook mode.";
            }

            if (!SecretPattern().IsMatch(WebhookSecret))
            {
                return "Telegram:WebhookSecret must be 1 to 256 letters, digits, underscore or dash in Webhook mode.";
            }
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,256}$")]
    private static partial Regex SecretPattern();
}
