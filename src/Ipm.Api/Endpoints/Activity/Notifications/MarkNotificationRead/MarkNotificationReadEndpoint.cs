using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Notifications.Commands.MarkNotificationRead;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Notifications.MarkNotificationRead;

internal sealed class MarkNotificationReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("notifications/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new MarkNotificationReadCommand(id), ct)).ToHttpResult())
            .WithTags(ActivityTags.Notifications)
            .WithSummary("تعليم إشعار كمقروء")
            .WithDescription("مرة ثانية ما بتغيّر وقت القراءة. إشعار غير موجود أو لغيرك ← 404.");
}
