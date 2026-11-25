using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkflowOverrides;
using Ipm.Domain.Projects;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.SetProjectWorkflowOverrides;

internal sealed class SetProjectWorkflowOverridesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("projects/{id:guid}/workflow-overrides", async (Guid id, SetProjectWorkflowOverridesRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                var overrides = (r.Overrides ?? []).Select(o => new WorkflowOverride(o.Key, o.Value)).ToList();
                return (await sender.Send(new SetProjectWorkflowOverridesCommand(id, overrides, version), ct))
                    .ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("استثناءات سير العمل بالمشروع (If-Match)")
            .WithDescription("بصلاحية project.workitemtypes.configure داخل المشروع. { overrides: { typeId: workflowId } } هي القائمة الكاملة. سير عمل للأعمال فقط، وإذا هو سير عمل النوع نفسه بيتشال الاستثناء.");
}
