using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Queries.GetProjectById;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.GetProjectById;

internal sealed class GetProjectByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("projects/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetProjectByIdQuery(id), ct)).ToETagResult(response, p => p.Version))
            .WithTags(ProjectsTags.Projects)
            .WithSummary("مشروع واحد بأعضائه وأنواعه (ETag)")
            .WithDescription("مشروع ما بتراه بيرجع 404 (مش 403) حتى ما ينكشف وجوده.");
}
