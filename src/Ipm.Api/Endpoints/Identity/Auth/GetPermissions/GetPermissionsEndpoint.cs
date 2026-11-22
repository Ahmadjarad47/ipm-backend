using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Auth.Queries.GetPermissions;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Auth.GetPermissions;

internal sealed class GetPermissionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("permissions", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPermissionsQuery(), ct)).ToHttpResult())
            .WithTags(IdentityTags.Roles)
            .WithSummary("مفاتيح الصلاحيات الـ 42 مجمّعة بأسمائها العربية");
}
