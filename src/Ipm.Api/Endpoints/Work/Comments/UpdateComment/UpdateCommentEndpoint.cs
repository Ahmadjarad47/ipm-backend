using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Comments.Commands.UpdateComment;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Comments.UpdateComment;

internal sealed class UpdateCommentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("comments/{id:guid}", async (Guid id, UpdateCommentRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new UpdateCommentCommand(id, r.Body, version), ct)).ToETagResult(http.Response, c => c.Version);
            })
            .WithTags(WorkTags.Comments)
            .WithSummary("تعديل تعليقك (If-Match)")
            .WithDescription("بصلاحية comments.edit_own، للمؤلف فقط وخلال 24 ساعة من إنشاء التعليق. بيضيف نسخة لسجل النصوص.");
}
