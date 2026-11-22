using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.ReopenWorkItem;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.ReopenWorkItem;

internal sealed class ReopenWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items/{id:guid}/reopen", async (Guid id, ReopenWorkItemRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new ReopenWorkItemCommand(id, r.ToStatusId, r.Comment, version), ct)).ToETagResult(http.Response, i => i.Version);
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("إعادة فتح عنصر منجز أو ملغى (If-Match)")
            .WithDescription("السبب (comment) إلزامي. بيروح لأول حالة مفتوحة يسمح فيها سير العمل، أو للحالة المحددة بـ toStatusId.");
}
