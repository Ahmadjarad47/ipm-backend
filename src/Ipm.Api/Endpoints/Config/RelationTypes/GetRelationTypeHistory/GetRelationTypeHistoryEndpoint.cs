using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypeHistory;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.RelationTypes.GetRelationTypeHistory;

internal sealed class GetRelationTypeHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("relation-types/{id:guid}/history", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetRelationTypeHistoryQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.WorkItemRelationTypesManage)
            .WithTags(ConfigTags.RelationTypes)
            .WithSummary("سجل تعديل نوع الرابط (الأحدث أولاً)")
            .WithDescription("من سجل التدقيق: وقت التعديل والمستخدم والقيم قبل وبعد. pageSize حتى 200.");
}
