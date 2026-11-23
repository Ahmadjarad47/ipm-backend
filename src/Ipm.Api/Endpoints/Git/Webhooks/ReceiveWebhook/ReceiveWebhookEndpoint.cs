using System.Text;
using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Webhooks.Commands.ReceiveWebhook;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Webhooks.ReceiveWebhook;

/// <summary>R5 and R6: no login, the signature is the credential. The raw body is read as is (the signature covers its exact bytes), capped at 1 MB.</summary>
internal sealed class ReceiveWebhookEndpoint : IEndpoint
{
    private const int MaxBytes = 1024 * 1024;

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("webhooks/{provider}/{repositoryId:guid}", async (string provider, Guid repositoryId, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ContentLength > MaxBytes)
                {
                    return TooLarge();
                }

                using var buffer = new MemoryStream();
                var chunk = new byte[16 * 1024];
                int read;
                while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
                {
                    await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
                    if (buffer.Length > MaxBytes)
                    {
                        return TooLarge();
                    }
                }

                var headers = http.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
                var raw = Encoding.UTF8.GetString(buffer.ToArray());
                return (await sender.Send(new ReceiveWebhookCommand(provider, repositoryId, raw, headers), ct)).ToHttpResult();
            })
            .AllowAnonymous()
            .WithTags(GitTags.Webhooks)
            .WithSummary("استقبال ويب هوك من Gitea أو GitHub أو GitLab")
            .WithDescription("بدون تسجيل دخول: التوقيع هو الاعتماد. provider: gitea أو github أو gitlab. التوقيع HMAC-SHA256 على timestamp.body مع الهيدر X-Delivery-Timestamp (نافذة 5 دقائق)؛ GitLab بيبعث السرّ بـ X-Gitlab-Token. الحد 1 ميغا (413). التسليم المكرر بيرجع 200 وما بيتعالج مرتين.");

    private static IResult TooLarge() =>
        Results.Problem(detail: "حجم الطلب أكبر من الحد المسموح (1 ميغابايت).", statusCode: StatusCodes.Status413PayloadTooLarge, extensions: new Dictionary<string, object?> { ["code"] = "Git.PayloadTooLarge" });
}
