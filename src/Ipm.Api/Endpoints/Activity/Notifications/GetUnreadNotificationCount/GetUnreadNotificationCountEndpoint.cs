using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Notifications.Queries.GetUnreadNotificationCount;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Notifications.GetUnreadNotificationCount;

internal sealed class GetUnreadNotificationCountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("notifications/unread-count", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetUnreadNotificationCountQuery(), ct)).ToHttpResult())
            .WithTags(ActivityTags.Notifications)
            .WithSummary("عدّاد غير المقروء")
            .WithDescription("خفيف للشارة: unread فقط.");
}
