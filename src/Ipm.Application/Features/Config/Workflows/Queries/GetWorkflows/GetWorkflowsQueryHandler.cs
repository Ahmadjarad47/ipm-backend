using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Workflows.Queries.GetWorkflows;

internal sealed class GetWorkflowsQueryHandler(IApplicationDbContext db) : IQueryHandler<GetWorkflowsQuery, IReadOnlyList<WorkflowResponse>>
{
    public async ValueTask<Result<IReadOnlyList<WorkflowResponse>>> Handle(GetWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var workflows = db.Workflows.AsNoTracking();
        if (query.Scope is { } scope)
        {
            workflows = workflows.Where(w => w.Scope == scope);
        }

        var rows = await workflows.OrderBy(w => w.Scope).ThenBy(w => w.Name).ToListAsync(cancellationToken);
        IReadOnlyList<WorkflowResponse> items = [.. rows.Select(w => w.ToResponse())];
        return Result.Success(items);
    }
}
