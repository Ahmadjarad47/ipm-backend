using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Telegram.Commands.UnlinkTelegram;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Telegram.UnlinkTelegram;

internal sealed class UnlinkTelegramEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("telegram/link", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new UnlinkTelegramCommand(), ct)).ToHttpResult())
            .WithTags(ActivityTags.Telegram)
            .WithSummary("فك ربط تلغرام")
            .WithDescription("204 دايماً، حتى لو ما في ربط. بعدها ما بيدخل إشعار جديد للصادر.");
}
