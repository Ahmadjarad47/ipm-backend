using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Workflows.Queries.GetWorkflowById;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Workflows.GetWorkflowById;

internal sealed class GetWorkflowByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("workflows/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetWorkflowByIdQuery(id), ct)).ToETagResult(response, w => w.Version))
            .WithTags(ConfigTags.Workflows)
            .WithSummary("سير عمل واحد (ETag)");
}
