using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Workflows.Commands.CreateWorkflow;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Workflows.CreateWorkflow;

internal sealed class CreateWorkflowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("workflows", async (CreateWorkflowRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateWorkflowCommand(r.Key, r.Name, r.Scope, r.StatusIds ?? [], r.InitialStatusId, r.Transitions ?? []), ct))
                    .ToCreatedETagResult(response, w => w.Version, w => $"/api/v1/workflows/{w.Id}"))
            .RequireAuthorization(Permissions.WorkflowsManage)
            .WithTags(ConfigTags.Workflows)
            .WithSummary("إنشاء سير عمل")
            .WithDescription("كل الحالات من نفس نطاق سير العمل والمفعّلة، والابتدائية منها. كل انتقال: { fromStatusId, toStatusId, permission?, requiresAssignee, requiresApproval, blockedByOpenDependencies }. أخطاء الانتقالات بمسارها الدقيق مثل transitions[2].toStatusId.");
}
