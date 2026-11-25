using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.CloseWorkItem;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.CloseWorkItem;

internal sealed class CloseWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items/{id:guid}/close", async (Guid id, CloseWorkItemRequest? r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new CloseWorkItemCommand(id, r?.ToStatusId, r?.Comment, version), ct)).ToETagResult(http.Response, i => i.Version);
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("إغلاق عمل (If-Match)")
            .WithDescription("بيروح لحالة من تصنيف مكتمل معرّفة من الحالة الحالية. إذا فيه وحدة بس بتنختار لحالها، وإذا أكتر لازم toStatusId (400 وإلا).");
}
