using Ipm.Application.Abstractions.Telegram;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Telegram;

/// <summary><see cref="ITelegramSettings"/> over the <c>Telegram</c> options. Never exposes the token.</summary>
internal sealed class TelegramSettings(IOptions<TelegramOptions> options) : ITelegramSettings
{
    public bool Enabled => options.Value.IsActive;

    public string BotUsername => options.Value.BotUsername.TrimStart('@');

    public string AppBaseUrl => options.Value.AppBaseUrl;

    public string WebhookSecret => options.Value.WebhookSecret;
}
