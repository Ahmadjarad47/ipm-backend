using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Common;

internal static class WorkflowMapping
{
    public static WorkflowResponse ToResponse(this Workflow workflow) => new(
        workflow.Id,
        workflow.Key,
        workflow.Name,
        workflow.Scope,
        workflow.StatusIds,
        workflow.InitialStatusId,
        workflow.Transitions,
        workflow.Version);
}
