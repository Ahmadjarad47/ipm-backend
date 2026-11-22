using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.Comments.Queries.GetComments;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Comments.GetComments;

internal sealed class GetCommentsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("comments", async (string subjectType, Guid subjectId, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetCommentsQuery(subjectType, subjectId, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(WorkTags.Comments)
            .WithSummary("تعليقات مشروع أو عنصر (الأقدم أولاً)")
            .WithDescription("subjectType: project أو work-item. لأي عضو بيشوف المشروع. التعليق المحذوف بيرجع بدون نص. pageSize حتى 200.");
}
