using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.SetWorkItemParent;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.SetWorkItemParent;

internal sealed class SetWorkItemParentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("work-items/{id:guid}/parent", async (Guid id, SetWorkItemParentRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetWorkItemParentCommand(id, r.ParentId, version), ct)).ToETagResult(http.Response, i => i.Version);
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("تغيير الأب (If-Match)")
            .WithDescription("بصلاحية tasks.edit. الأب من نفس المشروع ونوعه بيسمح بنوع العمل ابناً، وبدون حلقة. parentId = null بيشيل الأب.");
}
