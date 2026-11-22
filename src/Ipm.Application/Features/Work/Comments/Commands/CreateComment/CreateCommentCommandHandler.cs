using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Comments.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Commands.CreateComment;

internal sealed class CreateCommentCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<CreateCommentCommand, CommentResponse>
{
    public async ValueTask<Result<CommentResponse>> Handle(CreateCommentCommand command, CancellationToken cancellationToken)
    {
        var guarded = await CommentSubjects.GuardAsync(
            db, currentUser, access, securityLog, command.SubjectType, command.SubjectId, Permissions.CommentsCreate, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (project, caller) = (guarded.Value.Project, guarded.Value.Caller);
        if (project.IsArchived)
        {
            return CommentErrors.ProjectArchived;
        }

        var mentions = await CommentSubjects.MentionsAsync(db, project, command.Body, cancellationToken);
        var comment = Comment.Create(command.SubjectType, command.SubjectId, caller.UserId, command.Body, mentions, time.GetUtcNow());
        db.Comments.Add(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return comment.ToResponse();
    }
}
