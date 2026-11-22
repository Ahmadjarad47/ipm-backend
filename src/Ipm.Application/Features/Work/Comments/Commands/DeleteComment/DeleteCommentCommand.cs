using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Work.Comments.Commands.DeleteComment;

/// <summary>Deletes a comment without removing it (R15): its author, or a holder of files.delete in the project.</summary>
public sealed record DeleteCommentCommand(Guid Id, uint ExpectedVersion) : ICommand;
