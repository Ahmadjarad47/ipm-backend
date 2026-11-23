using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Activity.Audit.Queries.VerifyAudit;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Audit.VerifyAudit;

internal sealed class VerifyAuditEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("audit/verify", async (ISender sender, CancellationToken ct) => (await sender.Send(new VerifyAuditQuery(), ct)).ToHttpResult())
            .WithTags(ActivityTags.Audit)
            .WithSummary("فحص سلامة سجل التدقيق")
            .WithDescription("بصلاحية audit.view. بيعيد حساب سلسلة الـ hash: ok وchecked، وإذا انكسرت brokenAt = معرّف أول حدث ما بيطابق.");
}
