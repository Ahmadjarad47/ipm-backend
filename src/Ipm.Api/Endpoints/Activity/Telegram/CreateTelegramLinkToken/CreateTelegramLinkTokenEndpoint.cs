using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Telegram.Commands.CreateTelegramLinkToken;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Telegram.CreateTelegramLinkToken;

internal sealed class CreateTelegramLinkTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("telegram/link", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateTelegramLinkTokenCommand(), ct)).ToHttpResult())
            .WithTags(ActivityTags.Telegram)
            .WithSummary("بدء ربط تلغرام")
            .WithDescription("بدون جسم. بيرجّع { deepLink, expiresAt }: رابط https://t.me/<bot>?start=<token> يُفتح بتلغرام ويُضغط Start. الرمز لمرة وحدة وصالح 10 دقائق، ورمز جديد بيبطل القديم. الميزة معطّلة ← 422 Telegram.Disabled.");
}
