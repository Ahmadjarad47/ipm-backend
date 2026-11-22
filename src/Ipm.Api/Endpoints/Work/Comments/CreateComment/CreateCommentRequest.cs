using Ipm.Domain.Work;

namespace Ipm.Api.Endpoints.Work.Comments.CreateComment;

public sealed record CreateCommentRequest(CommentSubjectType SubjectType, Guid SubjectId, string Body);
