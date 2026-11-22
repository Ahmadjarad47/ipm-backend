using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Common;

/// <summary>A comment. A deleted one has no text and no mentions. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record CommentResponse(
    Guid Id,
    CommentSubjectType SubjectType,
    Guid SubjectId,
    Guid AuthorId,
    string Body,
    bool Edited,
    bool Deleted,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTimeOffset CreatedAt,
    uint Version);
