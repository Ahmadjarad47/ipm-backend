using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.ArchiveProject;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.ArchiveProject;

internal sealed class ArchiveProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{id:guid}/archive", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new ArchiveProjectCommand(id, version), ct)).ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("أرشفة المشروع (If-Match)")
            .WithDescription("بصلاحية projects.archive داخل المشروع. المؤرشف للقراءة فقط لحد ما يُستعاد.");
}
