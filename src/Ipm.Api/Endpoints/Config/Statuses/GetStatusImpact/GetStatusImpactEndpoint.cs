using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Statuses.Queries.GetStatusImpact;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Statuses.GetStatusImpact;

internal sealed class GetStatusImpactEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("statuses/{id:guid}/impact", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetStatusImpactQuery(id), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.StatusesManage)
            .WithTags(ConfigTags.Statuses)
            .WithSummary("معاينة أثر تغيير الحالة (قراءة فقط)")
            .WithDescription("عدد الأعمال والمشاريع وسير العمل اللي فيها الحالة، وسير العمل اللي هي ابتدائية فيها. للاطلاع قبل التعطيل أو الإزالة.");
}
