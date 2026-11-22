using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Identity.Roles.Queries.GetRoles;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Roles.GetRoles;

internal sealed class GetRolesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("roles", async (ISender sender, CancellationToken ct, string? scope = null, int page = 1, int pageSize = 50) =>
            {
                var parsed = KebabCaseEnum.ParseQuery<RoleScope>(scope, "scope", out var problem);
                return problem ?? (await sender.Send(new GetRolesQuery(parsed, new PageRequest(page, pageSize)), ct)).ToHttpResult();
            })
            .WithTags(IdentityTags.Roles)
            .WithSummary("الأدوار مع عدد صلاحياتها وعدد مستخدميها")
            .WithDescription("scope: system أو project. المدمجة أولاً ثم بالاسم. pageSize حتى 200.");
}
