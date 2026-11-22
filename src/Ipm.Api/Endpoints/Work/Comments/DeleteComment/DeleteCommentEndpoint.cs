using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Comments.Commands.DeleteComment;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Comments.DeleteComment;

internal sealed class DeleteCommentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("comments/{id:guid}", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new DeleteCommentCommand(id, version), ct)).ToHttpResult();
            })
            .WithTags(WorkTags.Comments)
            .WithSummary("حذف تعليق (If-Match)")
            .WithDescription("المؤلف، أو من معه files.delete بالمشروع. حذف ناعم: التعليق بيرجع للقراء بدون نص، والنص الأصلي بيضل للتدقيق.");
}
