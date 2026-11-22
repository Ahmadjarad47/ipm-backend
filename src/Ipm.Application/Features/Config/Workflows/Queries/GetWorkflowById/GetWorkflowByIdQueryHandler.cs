using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Workflows.Queries.GetWorkflowById;

internal sealed class GetWorkflowByIdQueryHandler(IApplicationDbContext db) : IQueryHandler<GetWorkflowByIdQuery, WorkflowResponse>
{
    public async ValueTask<Result<WorkflowResponse>> Handle(GetWorkflowByIdQuery query, CancellationToken cancellationToken)
    {
        var workflow = await db.Workflows.AsNoTracking().SingleOrDefaultAsync(w => w.Id == query.Id, cancellationToken);
        return workflow is null ? WorkflowErrors.NotFound : workflow.ToResponse();
    }
}
