using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.Comments.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Comments.Queries.GetComments;

internal sealed class GetCommentsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetCommentsQuery, PagedList<CommentResponse>>
{
    public async ValueTask<Result<PagedList<CommentResponse>>> Handle(GetCommentsQuery query, CancellationToken cancellationToken)
    {
        var type = CommentSubjects.TryParse(query.SubjectType, out var parsed) ? parsed : default;
        var guarded = await CommentSubjects.GuardAsync(db, currentUser, access, securityLog, type, query.SubjectId, Permissions.ProjectsView, track: false, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var comments = db.Comments.AsNoTracking().Where(c => c.SubjectType == type && c.SubjectId == query.SubjectId);
        var total = await comments.CountAsync(cancellationToken);
        var rows = await comments.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip(query.Page.Skip).Take(query.Page.Take).ToListAsync(cancellationToken);
        return new PagedList<CommentResponse>([.. rows.Select(c => c.ToResponse())], Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
