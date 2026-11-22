using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Comments.Common;

namespace Ipm.Application.Features.Work.Comments.Commands.UpdateComment;

/// <summary>Edits the text of your own comment inside the edit window (R14).</summary>
public sealed record UpdateCommentCommand(Guid Id, string Body, uint ExpectedVersion) : ICommand<CommentResponse>;
