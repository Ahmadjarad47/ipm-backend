using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.CreateApprovalTemplate;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.CreateApprovalTemplate;

internal sealed class CreateApprovalTemplateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approval-templates", async (CreateApprovalTemplateRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateApprovalTemplateCommand(r.Name, r.Event, r.Active, r.Stages ?? [], r.ProtectedFields ?? [], r.Rule, r.AllowSelfApproval ?? false), ct))
                    .ToCreatedETagResult(response, t => t.Version, t => $"/api/v1/approval-templates/{t.Id}"))
            .RequireAuthorization(Permissions.ApprovalsConfigure)
            .WithTags(ApprovalsTags.Templates)
            .WithSummary("إنشاء قالب موافقة")
            .WithDescription("بصلاحية approvals.configure. event: project-approve أو project-close أو work-item-approve أو sensitive-change. المراحل بترتيب متسلسل من 1، والمعتمد user أو role أو position (project-owner أو direct-manager أو project-manager). rule اختياري وكل أجزائه اختيارية.");
}
