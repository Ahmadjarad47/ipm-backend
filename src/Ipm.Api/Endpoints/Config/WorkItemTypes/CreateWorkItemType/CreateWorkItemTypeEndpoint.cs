using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Commands.CreateWorkItemType;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.CreateWorkItemType;

internal sealed class CreateWorkItemTypeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-item-types", async (CreateWorkItemTypeRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateWorkItemTypeCommand(r.Key, r.Name, r.Description ?? string.Empty, r.Color, r.Icon, r.Order ?? 0, r.Active ?? true, r.WorkflowId), ct))
                    .ToCreatedETagResult(response, t => t.Version, t => $"/api/v1/work-item-types/{t.Id}"))
            .RequireAuthorization(Permissions.WorkItemTypesManage)
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("إنشاء نوع عمل")
            .WithDescription("المفتاح بأحرف إنجليزية صغيرة وأرقام و_ ويبدأ بحرف، وبيتحوّل لأحرف صغيرة، وما بيتغيّر بعد الإنشاء. الحقول والأبناء بروابطهم الخاصة.");
}
