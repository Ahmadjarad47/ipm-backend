using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Notifications.Commands.MarkNotificationsRead;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Notifications.MarkNotificationsRead;

internal sealed class MarkNotificationsReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("notifications/read-batch", async (MarkNotificationsReadRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new MarkNotificationsReadCommand(request.Ids), ct)).ToHttpResult())
            .WithTags(ActivityTags.Notifications)
            .WithSummary("تعليم عدّة إشعارات كمقروءة")
            .WithDescription("لإشعارات ظهرت على الشاشة دفعة واحدة. إشعار لغيرك أو مقروء من قبل يُتجاهل بصمت.");
}
