using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Comments.Common;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Commands.CreateComment;

/// <summary>Adds a comment to a project or a work item (R13).</summary>
public sealed record CreateCommentCommand(CommentSubjectType SubjectType, Guid SubjectId, string Body) : ICommand<CommentResponse>;
