using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.TransitionWorkItem;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.TransitionWorkItem;

internal sealed class TransitionWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items/{id:guid}/transition", async (Guid id, TransitionWorkItemRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new TransitionWorkItemCommand(id, r.ToStatusId, r.Comment, version), ct)).ToETagResult(http.Response, i => i.Version);
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("نقل عنصر إلى حالة أخرى عبر سير العمل (If-Match)")
            .WithDescription("بصلاحية workflow.transition داخل المشروع (والصلاحية الإضافية للانتقال إن وُجدت). الانتقال لازم يكون معرّفاً بسير العمل الفعلي، وشرط المسؤول لازم يتحقق. الانتقال اللي بيتطلب موافقة بيتنفّذ فقط بصلاحية audit.override لحد ما ينبني محرك الموافقات، وبيتسجّل.");
}
