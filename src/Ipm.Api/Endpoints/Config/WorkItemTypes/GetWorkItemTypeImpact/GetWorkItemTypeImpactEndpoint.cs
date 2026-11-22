using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeImpact;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.GetWorkItemTypeImpact;

internal sealed class GetWorkItemTypeImpactEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-item-types/{id:guid}/impact", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetWorkItemTypeImpactQuery(id), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.WorkItemTypesManage)
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("معاينة أثر تغيير النوع (قراءة فقط)")
            .WithDescription("عدد العناصر والمشاريع المسموح فيها وسير العمل المستعملة، والمشاريع اللي النوع افتراضي فيها. للاطلاع قبل التعطيل أو تغيير الربط.");
}
