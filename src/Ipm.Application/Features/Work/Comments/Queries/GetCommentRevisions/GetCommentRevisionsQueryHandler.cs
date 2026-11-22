using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Comments.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Comments.Queries.GetCommentRevisions;

internal sealed class GetCommentRevisionsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetCommentRevisionsQuery, IReadOnlyList<CommentRevision>>
{
    public async ValueTask<Result<IReadOnlyList<CommentRevision>>> Handle(GetCommentRevisionsQuery query, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
        if (comment is null)
        {
            return CommentErrors.NotFound;
        }

        var guarded = await CommentSubjects.GuardAsync(
            db, currentUser, access, securityLog, comment.SubjectType, comment.SubjectId, Permissions.ProjectsView, track: false, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == CommentErrors.SubjectNotFound ? CommentErrors.NotFound : guarded.Error;
        }

        return comment.IsDeleted ? [] : comment.Revisions;
    }
}
