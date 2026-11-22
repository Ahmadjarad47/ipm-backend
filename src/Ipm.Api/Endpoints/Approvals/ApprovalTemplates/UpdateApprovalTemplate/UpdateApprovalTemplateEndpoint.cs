using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.UpdateApprovalTemplate;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.UpdateApprovalTemplate;

internal sealed class UpdateApprovalTemplateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("approval-templates/{id:guid}", async (Guid id, UpdateApprovalTemplateRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateApprovalTemplateCommand(
                            id, r.Name, r.Event, r.Active, r.Stages ?? [], r.ProtectedFields ?? [], r.Rule, r.AllowSelfApproval ?? false, version), ct))
                    .ToETagResult(http.Response, t => t.Version);
            })
            .RequireAuthorization(Permissions.ApprovalsConfigure)
            .WithTags(ApprovalsTags.Templates)
            .WithSummary("تعديل قالب موافقة (If-Match)")
            .WithDescription("بصلاحية approvals.configure. الطلبات الجارية بتحتفظ بنسختها من المراحل. الحدث ما بيتغيّر بعد ما تتقدّم طلبات من القالب. active=false بيعطّله (ما بينحذف).");
}
