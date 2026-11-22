using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.ProjectTemplates.Queries.GetProjectTemplates;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.ProjectTemplates.GetProjectTemplates;

internal sealed class GetProjectTemplatesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("project-templates", async (ISender sender, CancellationToken ct, bool? active = null) =>
                (await sender.Send(new GetProjectTemplatesQuery(active), ct)).ToHttpResult())
            .WithTags(ProjectsTags.Templates)
            .WithSummary("قوالب المشاريع")
            .WithDescription("active=true للمفعّلة فقط. بدون ترقيم (قائمة إعدادات قصيرة).");
}
