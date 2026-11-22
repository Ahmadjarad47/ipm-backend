using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeHistory;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.GetWorkItemTypeHistory;

internal sealed class GetWorkItemTypeHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-item-types/{id:guid}/history", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetWorkItemTypeHistoryQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.WorkItemTypesManage)
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("سجل تعديل النوع (الأحدث أولاً)")
            .WithDescription("من سجل التدقيق: وقت التعديل والمستخدم والقيم قبل وبعد (الأعمدة المتغيّرة فقط عند التعديل). pageSize حتى 200.");
}
