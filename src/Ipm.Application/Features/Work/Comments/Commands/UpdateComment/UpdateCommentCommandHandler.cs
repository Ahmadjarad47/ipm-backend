using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Comments.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Comments.Commands.UpdateComment;

internal sealed class UpdateCommentCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<UpdateCommentCommand, CommentResponse>
{
    public async ValueTask<Result<CommentResponse>> Handle(UpdateCommentCommand command, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (comment is null)
        {
            return CommentErrors.NotFound;
        }

        var guarded = await CommentSubjects.GuardAsync(
            db, currentUser, access, securityLog, comment.SubjectType, comment.SubjectId, Permissions.CommentsEditOwn, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == CommentErrors.SubjectNotFound ? CommentErrors.NotFound : guarded.Error;
        }

        var (project, caller) = (guarded.Value.Project, guarded.Value.Caller);
        if (!comment.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (project.IsArchived)
        {
            return CommentErrors.ProjectArchived;
        }

        var mentions = await CommentSubjects.MentionsAsync(db, project, command.Body, cancellationToken);
        var edited = comment.Edit(caller.UserId, command.Body, mentions, time.GetUtcNow(), (await db.CurrentSettingsAsync(cancellationToken)).CommentEditHours);
        if (edited.IsFailure)
        {
            return edited.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return comment.ToResponse();
    }
}
