using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeChildren;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.SetWorkItemTypeChildren;

internal sealed class SetWorkItemTypeChildrenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("work-item-types/{id:guid}/children", async (Guid id, SetWorkItemTypeChildrenRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetWorkItemTypeChildrenCommand(id, r.ChildTypeIds ?? [], version), ct))
                    .ToETagResult(http.Response, t => t.Version);
            })
            .RequireAuthorization(Permissions.WorkItemTypesManage)
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("تعديل الأنواع الفرعية المسموحة للنوع (If-Match)")
            .WithDescription("القائمة المرسلة هي القائمة الكاملة. مسموح النوع نفسه (مهمة داخل مهمة).");
}
