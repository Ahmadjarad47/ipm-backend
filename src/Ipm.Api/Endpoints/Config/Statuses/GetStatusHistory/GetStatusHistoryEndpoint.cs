using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Config.Statuses.Queries.GetStatusHistory;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Statuses.GetStatusHistory;

internal sealed class GetStatusHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("statuses/{id:guid}/history", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetStatusHistoryQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.StatusesManage)
            .WithTags(ConfigTags.Statuses)
            .WithSummary("سجل تعديل الحالة (الأحدث أولاً)")
            .WithDescription("من سجل التدقيق: وقت التعديل والمستخدم والقيم قبل وبعد. pageSize حتى 200.");
}
