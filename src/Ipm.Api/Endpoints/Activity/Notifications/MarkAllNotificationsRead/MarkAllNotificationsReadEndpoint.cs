using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Notifications.Commands.MarkAllNotificationsRead;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Notifications.MarkAllNotificationsRead;

internal sealed class MarkAllNotificationsReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("notifications/read-all", async (ISender sender, CancellationToken ct, Guid? projectId = null) =>
                (await sender.Send(new MarkAllNotificationsReadCommand(projectId), ct)).ToHttpResult())
            .WithTags(ActivityTags.Notifications)
            .WithSummary("تعليم كل الإشعارات كمقروءة")
            .WithDescription("لكل إشعاراتك، أو لمشروع واحد بـ projectId.");
}
