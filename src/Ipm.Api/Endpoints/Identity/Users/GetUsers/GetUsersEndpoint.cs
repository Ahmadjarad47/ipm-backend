using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Identity.Users.Queries.GetUsers;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.GetUsers;

internal sealed class GetUsersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("users", async (
                    ISender sender,
                    CancellationToken ct,
                    string? q = null,
                    bool? active = null,
                    Guid? roleId = null,
                    int page = 1,
                    int pageSize = 50) =>
                (await sender.Send(new GetUsersQuery(q, active, roleId, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("المستخدمون الكاملون مع البحث والترقيم")
            .WithDescription("q يبحث في الاسم واسم المستخدم والبريد. pageSize حتى 200.");
}
