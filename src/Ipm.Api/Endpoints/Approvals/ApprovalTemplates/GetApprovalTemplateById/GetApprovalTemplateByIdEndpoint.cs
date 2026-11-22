using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.GetApprovalTemplateById;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.GetApprovalTemplateById;

internal sealed class GetApprovalTemplateByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("approval-templates/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetApprovalTemplateByIdQuery(id), ct)).ToETagResult(response, t => t.Version))
            .WithTags(ApprovalsTags.Templates)
            .WithSummary("قالب موافقة واحد (ETag)");
}
