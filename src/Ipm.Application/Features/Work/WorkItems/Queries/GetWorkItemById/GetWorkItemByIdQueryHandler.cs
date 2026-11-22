using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemById;

internal sealed class GetWorkItemByIdQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetWorkItemByIdQuery, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(GetWorkItemByIdQuery query, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, query.Id, Permissions.TasksView, cancellationToken);
        return guarded.IsFailure ? guarded.Error : guarded.Value.Item.ToResponse();
    }
}
