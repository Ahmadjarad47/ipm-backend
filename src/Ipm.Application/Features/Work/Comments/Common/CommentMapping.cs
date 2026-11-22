using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Common;

internal static class CommentMapping
{
    public static CommentResponse ToResponse(this Comment comment) => new(
        comment.Id,
        comment.SubjectType,
        comment.SubjectId,
        comment.AuthorId,
        comment.IsDeleted ? string.Empty : comment.Body,
        comment.IsEdited,
        comment.IsDeleted,
        comment.IsDeleted ? [] : comment.MentionedUserIds,
        comment.CreatedAt,
        comment.Version);
}
