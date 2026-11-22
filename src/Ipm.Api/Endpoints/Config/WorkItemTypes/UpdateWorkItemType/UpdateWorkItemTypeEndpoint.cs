using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Commands.UpdateWorkItemType;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.UpdateWorkItemType;

internal sealed class UpdateWorkItemTypeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("work-item-types/{id:guid}", async (Guid id, UpdateWorkItemTypeRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateWorkItemTypeCommand(id, r.Key, r.Name, r.Description ?? string.Empty, r.Color, r.Icon, r.Order, r.Active, r.WorkflowId, version), ct))
                    .ToETagResult(http.Response, t => t.Version);
            })
            .RequireAuthorization(Permissions.WorkItemTypesManage)
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("تعديل نوع عمل وتفعيله أو تعطيله (If-Match)")
            .WithDescription("الاسم والوصف واللون والأيقونة والترتيب والتفعيل. الأنواع ما بتنحذف، بتتعطّل. الحقول والأبناء بروابطهم الخاصة.");
}
