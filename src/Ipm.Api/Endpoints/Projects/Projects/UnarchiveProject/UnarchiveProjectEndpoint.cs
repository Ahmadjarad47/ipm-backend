using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.UnarchiveProject;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.UnarchiveProject;

internal sealed class UnarchiveProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{id:guid}/unarchive", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new UnarchiveProjectCommand(id, version), ct)).ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("استعادة مشروع مؤرشف (If-Match)")
            .WithDescription("بصلاحية projects.archive داخل المشروع.");
}
