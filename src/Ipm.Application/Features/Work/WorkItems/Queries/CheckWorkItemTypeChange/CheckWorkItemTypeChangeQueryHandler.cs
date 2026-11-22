using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Work.WorkItems.Queries.CheckWorkItemTypeChange;

internal sealed class CheckWorkItemTypeChangeQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<CheckWorkItemTypeChangeQuery, TypeChangeCheckResponse>
{
    public async ValueTask<Result<TypeChangeCheckResponse>> Handle(CheckWorkItemTypeChangeQuery query, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, query.Id, Permissions.WorkItemTypesManage, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var conflicts = await TypeChangeAnalysis.ConflictsAsync(db, guarded.Value.Project, guarded.Value.Item, query.TypeId, cancellationToken);
        return new TypeChangeCheckResponse(conflicts);
    }
}
