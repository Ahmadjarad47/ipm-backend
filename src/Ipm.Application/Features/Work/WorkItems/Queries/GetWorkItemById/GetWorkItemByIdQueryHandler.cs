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
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (item, caller) = (guarded.Value.Item, guarded.Value.Caller);
        var response = item.ToResponse();

        // Part 07 R13: the development indicator is for those who may see Git in the project.
        if (!caller.Can(Permissions.GitView, item.ProjectId))
        {
            return response;
        }

        var dev = await DevIndicators.LoadAsync(db, [item], cancellationToken);
        return response with { Dev = dev[item.Id] };
    }
}
