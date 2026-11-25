using System.Text.Json;

namespace Ipm.Application.Abstractions.Telegram;

/// <summary>
/// The part of a Telegram update the system uses: a text message. Everything else (edits, stickers, other update types) has a null chat and is ignored,
/// but still carries its <see cref="UpdateId"/> so the poller can move past it.
/// </summary>
public sealed record TelegramUpdate(long UpdateId, long? ChatId, string? ChatType, string? Text, string? Username)
{
    /// <summary>Null when the body is not a Telegram update at all.</summary>
    public static TelegramUpdate? Parse(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            return Parse(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static TelegramUpdate? Parse(JsonElement update)
    {
        if (update.ValueKind != JsonValueKind.Object || !update.TryGetProperty("update_id", out var id) || !id.TryGetInt64(out var updateId))
        {
            return null;
        }

        if (!update.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("chat", out var chat) || chat.ValueKind != JsonValueKind.Object || !chat.TryGetProperty("id", out var chatId) || !chatId.TryGetInt64(out var chatIdValue))
        {
            return new TelegramUpdate(updateId, null, null, null, null);
        }

        var type = chat.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : null;
        var text = message.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String ? textElement.GetString() : null;
        string? username = null;
        if (message.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.Object
            && from.TryGetProperty("username", out var name) && name.ValueKind == JsonValueKind.String)
        {
            username = name.GetString();
        }

        return new TelegramUpdate(updateId, chatIdValue, type, text, username);
    }
}
