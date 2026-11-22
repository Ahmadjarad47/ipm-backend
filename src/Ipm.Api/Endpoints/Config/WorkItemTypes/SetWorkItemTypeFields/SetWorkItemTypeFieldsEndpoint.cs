using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeFields;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.SetWorkItemTypeFields;

internal sealed class SetWorkItemTypeFieldsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("work-item-types/{id:guid}/fields", async (Guid id, SetWorkItemTypeFieldsRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetWorkItemTypeFieldsCommand(id, r.Fields ?? [], version), ct))
                    .ToETagResult(http.Response, t => t.Version);
            })
            .RequireAuthorization(Permissions.WorkItemFieldsManage)
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("تعديل الحقول المخصصة للنوع (If-Match)")
            .WithDescription("القائمة المرسلة هي القائمة الكاملة (حتى 40 حقل). أخطاء كل حقل بمساره الدقيق مثل fields[1].options.");
}
