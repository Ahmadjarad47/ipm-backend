using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Comments.Queries.GetCommentRevisions;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Comments.GetCommentRevisions;

internal sealed class GetCommentRevisionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("comments/{id:guid}/revisions", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetCommentRevisionsQuery(id), ct)).ToHttpResult())
            .WithTags(WorkTags.Comments)
            .WithSummary("سجل نصوص التعليق")
            .WithDescription("النسخة الأولى هي النص الأصلي، وكل تعديل بيضيف نسخة. فاضي للتعليق المحذوف.");
}
