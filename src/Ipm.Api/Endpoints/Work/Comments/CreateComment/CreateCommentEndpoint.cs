using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Comments.Commands.CreateComment;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Comments.CreateComment;

internal sealed class CreateCommentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("comments", async (CreateCommentRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new CreateCommentCommand(r.SubjectType, r.SubjectId, r.Body), ct))
                    .ToCreatedETagResult(response, c => c.Version, c => $"/api/v1/comments?subjectType={(c.SubjectType == Ipm.Domain.Work.CommentSubjectType.Project ? "project" : "work-item")}&subjectId={c.SubjectId}"))
            .WithTags(WorkTags.Comments)
            .WithSummary("إضافة تعليق")
            .WithDescription("subjectType: project أو work-item. بصلاحية comments.create بالمشروع، والمشروع غير مؤرشف. النص حتى 5000 حرف. الإشارات @username للأعضاء النشطين بتتخزّن بـ mentionedUserIds.");
}
