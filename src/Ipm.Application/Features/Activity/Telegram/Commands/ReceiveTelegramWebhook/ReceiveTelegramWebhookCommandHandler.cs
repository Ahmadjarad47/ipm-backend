using System.Security.Cryptography;
using System.Text;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Application.Features.Activity.Telegram.Common;
using Ipm.Domain.Activity;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Activity.Telegram.Commands.ReceiveTelegramWebhook;

internal sealed class ReceiveTelegramWebhookCommandHandler(ITelegramSettings settings, TelegramUpdateProcessor processor) : ICommandHandler<ReceiveTelegramWebhookCommand>
{
    public async ValueTask<Result> Handle(ReceiveTelegramWebhookCommand command, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !SecretMatches(command.Secret, settings.WebhookSecret))
        {
            return TelegramErrors.SecretInvalid;
        }

        // Whatever the body is, a valid caller gets 200: Telegram would only send it again.
        if (TelegramUpdate.Parse(command.Raw) is { } update)
        {
            await processor.ProcessAsync(update, cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>Both sides are hashed first so the comparison takes the same time whatever their lengths and content.</summary>
    private static bool SecretMatches(string? given, string expected) =>
        expected.Length > 0
        && !string.IsNullOrEmpty(given)
        && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
