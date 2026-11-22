using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.ProjectTemplates.Queries.GetProjectTemplateById;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.ProjectTemplates.GetProjectTemplateById;

internal sealed class GetProjectTemplateByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("project-templates/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetProjectTemplateByIdQuery(id), ct)).ToETagResult(response, t => t.Version))
            .WithTags(ProjectsTags.Templates)
            .WithSummary("قالب مشروع واحد (ETag)");
}
