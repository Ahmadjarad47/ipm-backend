using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Telegram.Queries.GetMyTelegramLink;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Telegram.GetMyTelegramLink;

internal sealed class GetMyTelegramLinkEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("telegram/link", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyTelegramLinkQuery(), ct)).ToHttpResult())
            .WithTags(ActivityTags.Telegram)
            .WithSummary("حالة ربط تلغرام للمستخدم الحالي")
            .WithDescription("{ enabled, botUsername, linked, telegramUsername, linkedAt }. الميزة معطّلة ← enabled=false. الربط المعطّل (حظر البوت) ← linked=false.");
}
