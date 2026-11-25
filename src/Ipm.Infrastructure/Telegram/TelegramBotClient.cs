using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Domain.Common;

namespace Ipm.Infrastructure.Telegram;

/// <summary>
/// The Telegram Bot API over a typed <see cref="HttpClient"/> whose base address is <c>https://api.telegram.org/bot{token}/</c> (spec 11 R9 and R11).
/// The token is part of every URL, so this client is registered without HTTP loggers, and nothing here puts a URL, an exception message or a request
/// body into a result: a failure is described by a status code or an exception type only.
/// </summary>
public sealed class TelegramBotClient(HttpClient client) : ITelegramBot
{
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

    /// <summary>Only text messages are used (R10).</summary>
    private static readonly string[] AllowedUpdates = ["message"];

    public async Task<TelegramSendResult> SendMessageAsync(long chatId, string html, string? buttonUrl, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = html,
            ["parse_mode"] = "HTML",
            ["disable_web_page_preview"] = true,
        };
        if (buttonUrl is not null)
        {
            payload["reply_markup"] = new { inline_keyboard = new[] { new[] { new { text = "فتح في IPM", url = buttonUrl } } } };
        }

        try
        {
            using var response = await client.PostAsJsonAsync(new Uri("sendMessage", UriKind.Relative), payload, cancellationToken);
            return await ClassifyAsync(response, cancellationToken);
        }
        catch (Exception e) when (IsNetworkFailure(e, cancellationToken))
        {
            return TelegramSendResult.Transient(Describe(e));
        }
    }

    public async Task<Result<IReadOnlyList<TelegramUpdate>>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                new Uri("getUpdates", UriKind.Relative), new { offset, timeout = timeoutSeconds, allowed_updates = AllowedUpdates }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Error.BadGateway("Telegram.GetUpdatesFailed", $"رفض تلغرام الطلب ({(int)response.StatusCode}).");
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            {
                return Error.BadGateway("Telegram.GetUpdatesFailed", "ردّ تلغرام غير متوقع.");
            }

            return result.EnumerateArray().Select(TelegramUpdate.Parse).OfType<TelegramUpdate>().ToList();
        }
        catch (Exception e) when (IsNetworkFailure(e, cancellationToken))
        {
            return Error.BadGateway("Telegram.GetUpdatesFailed", $"تعذّر الاتصال بتلغرام ({Describe(e)}).");
        }
    }

    public Task<Result> SetWebhookAsync(string url, string secretToken, CancellationToken cancellationToken) =>
        CallAsync("setWebhook", new { url, secret_token = secretToken, allowed_updates = AllowedUpdates }, cancellationToken);

    public Task<Result> DeleteWebhookAsync(CancellationToken cancellationToken) =>
        CallAsync("deleteWebhook", new { drop_pending_updates = false }, cancellationToken);

    private async Task<Result> CallAsync(string method, object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(new Uri(method, UriKind.Relative), payload, cancellationToken);
            return response.IsSuccessStatusCode
                ? Result.Success()
                : Error.BadGateway("Telegram.CallFailed", $"رفض تلغرام الطلب ({(int)response.StatusCode}).");
        }
        catch (Exception e) when (IsNetworkFailure(e, cancellationToken))
        {
            return Error.BadGateway("Telegram.CallFailed", $"تعذّر الاتصال بتلغرام ({Describe(e)}).");
        }
    }

    /// <summary>R9: ok, 429 with retry_after, 403 or chat not found (blocked), 5xx (try again), any other refusal (give up).</summary>
    internal static async Task<TelegramSendResult> ClassifyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return TelegramSendResult.Ok;
        }

        var status = (int)response.StatusCode;
        var (description, retryAfter) = await ReadErrorAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return TelegramSendResult.After(retryAfter ?? DefaultRetryAfter);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden
            || (response.StatusCode == HttpStatusCode.BadRequest && description.Contains("chat not found", StringComparison.OrdinalIgnoreCase)))
        {
            return TelegramSendResult.Blocked($"Telegram {status}: {description}");
        }

        return status >= 500
            ? TelegramSendResult.Transient($"Telegram {status}")
            : TelegramSendResult.Permanent($"Telegram {status}: {description}");
    }

    private static async Task<(string Description, TimeSpan? RetryAfter)> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            var description = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;
            TimeSpan? retryAfter = null;
            if (root.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("retry_after", out var seconds) && seconds.TryGetInt32(out var value) && value > 0)
            {
                retryAfter = TimeSpan.FromSeconds(value);
            }

            return (description, retryAfter);
        }
        catch (JsonException)
        {
            return (string.Empty, null);
        }
    }

    /// <summary>A refused connection, a timeout or a bad body: not a shutdown (that one must still stop the caller).</summary>
    private static bool IsNetworkFailure(Exception e, CancellationToken cancellationToken) =>
        e is HttpRequestException or JsonException or IOException
        || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>The exception type only: its message could carry the address, which contains the token.</summary>
    private static string Describe(Exception e) => e is OperationCanceledException ? "timeout" : e.GetType().Name;
}
