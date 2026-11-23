using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Webhooks.Commands.RedeliverWebhook;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Webhooks.RedeliverWebhook;

internal sealed class RedeliverWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("git/deliveries/{id:guid}/redeliver", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RedeliverWebhookCommand(id), ct)).ToHttpResult())
            .WithTags(GitTags.Webhooks)
            .WithSummary("إعادة معالجة تسليم فاشل")
            .WithDescription("بصلاحية git.manage. للتسليمات الفاشلة فقط؛ غيرها ← 422.");
}
