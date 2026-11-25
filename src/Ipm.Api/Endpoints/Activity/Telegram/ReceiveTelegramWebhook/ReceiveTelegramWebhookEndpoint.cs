using System.Text;
using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Telegram.Commands.ReceiveTelegramWebhook;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Telegram.ReceiveTelegramWebhook;

/// <summary>R10: no login, the secret header is the credential. The body is capped at 64 KB.</summary>
internal sealed class ReceiveTelegramWebhookEndpoint : IEndpoint
{
    private const int MaxBytes = 64 * 1024;
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("telegram/webhook", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ContentLength > MaxBytes)
                {
                    return TooLarge();
                }

                using var buffer = new MemoryStream();
                var chunk = new byte[8 * 1024];
                int read;
                while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
                {
                    await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
                    if (buffer.Length > MaxBytes)
                    {
                        return TooLarge();
                    }
                }

                var secret = http.Request.Headers[SecretHeader].ToString();
                var result = await sender.Send(new ReceiveTelegramWebhookCommand(secret, Encoding.UTF8.GetString(buffer.ToArray())), ct);
                return result.IsSuccess ? Results.Ok() : result.Error.ToProblem();
            })
            .AllowAnonymous()
            .WithTags(ActivityTags.Telegram)
            .WithSummary("استقبال تحديثات تلغرام (webhook)")
            .WithDescription("بدون تسجيل دخول: الترويسة X-Telegram-Bot-Api-Secret-Token لازم تطابق Telegram:WebhookSecret (مقارنة ثابتة الزمن)، وإلا 401. الحد 64 كيلوبايت (413). بيرجّع 200 دايماً للتحديث المقبول.");

    private static IResult TooLarge() =>
        Results.Problem(detail: "حجم الطلب أكبر من الحد المسموح (64 كيلوبايت).", statusCode: StatusCodes.Status413PayloadTooLarge, extensions: new Dictionary<string, object?> { ["code"] = "Telegram.PayloadTooLarge" });
}
