using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Workflows.Commands.UpdateWorkflow;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Workflows.UpdateWorkflow;

internal sealed class UpdateWorkflowEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("workflows/{id:guid}", async (Guid id, UpdateWorkflowRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateWorkflowCommand(id, r.Key, r.Scope, r.Name, r.StatusIds ?? [], r.InitialStatusId, r.Transitions ?? [], version), ct))
                    .ToETagResult(http.Response, w => w.Version);
            })
            .RequireAuthorization(Permissions.WorkflowsManage)
            .WithTags(ConfigTags.Workflows)
            .WithSummary("تعديل سير عمل: الاسم والحالات والانتقالات (If-Match)")
            .WithDescription("القوائم المرسلة هي القوائم الكاملة. المفتاح والنطاق ثابتين. الحالة المعطّلة بتضل بسير العمل اللي فيه، وما بتنضاف لغيره.");
}
