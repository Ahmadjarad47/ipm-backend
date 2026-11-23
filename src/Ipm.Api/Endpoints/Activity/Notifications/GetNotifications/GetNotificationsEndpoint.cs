using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Activity.Notifications.Queries.GetNotifications;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Notifications.GetNotifications;

internal sealed class GetNotificationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("notifications", async (ISender sender, CancellationToken ct, bool unread = false, int page = 1, int pageSize = 30) =>
                (await sender.Send(new GetNotificationsQuery(unread, new PageRequest(page, Math.Min(pageSize, 100))), ct)).ToHttpResult())
            .WithTags(ActivityTags.Notifications)
            .WithSummary("إشعاراتي")
            .WithDescription("إشعارات المستخدم الحالي فقط، الأحدث أولاً. unread=true لغير المقروء. الرد: items وtotal وunread (العدّاد الكلي) وpage وpageSize. pageSize حتى 100.");
}
