using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Comments.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Comments.Commands.DeleteComment;

internal sealed class DeleteCommentCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<DeleteCommentCommand>
{
    public async ValueTask<Result> Handle(DeleteCommentCommand command, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (comment is null)
        {
            return CommentErrors.NotFound;
        }

        var guarded = await CommentSubjects.GuardAsync(
            db, currentUser, access, securityLog, comment.SubjectType, comment.SubjectId, Permissions.ProjectsView, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == CommentErrors.SubjectNotFound ? CommentErrors.NotFound : guarded.Error;
        }

        var (project, caller) = (guarded.Value.Project, guarded.Value.Caller);
        if (!comment.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (comment.AuthorId != caller.UserId)
        {
            var allowed = await WorkItemGuard.RequireAsync(caller, project.Id, Permissions.FilesDelete, securityLog, cancellationToken);
            if (allowed.IsFailure)
            {
                return allowed.Error;
            }
        }

        if (project.IsArchived)
        {
            return CommentErrors.ProjectArchived;
        }

        var deleted = comment.Delete();
        if (deleted.IsFailure)
        {
            return deleted.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
