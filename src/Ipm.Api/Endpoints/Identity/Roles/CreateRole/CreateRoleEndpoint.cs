using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Roles.Commands.CreateRole;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Roles.CreateRole;

internal sealed class CreateRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("roles", async (CreateRoleRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateRoleCommand(r.Name, r.Description ?? string.Empty, r.Scope, r.Permissions ?? [], r.Denied ?? [], r.CopyFromRoleId), ct))
                    .ToCreatedETagResult(response, role => role.Version, role => $"/api/v1/roles/{role.Id}"))
            .RequireAuthorization(Permissions.RolesManage)
            .WithTags(IdentityTags.Roles)
            .WithSummary("إنشاء دور")
            .WithDescription("ما بتقدر تمنح صلاحية ما بتملكها (403). النطاق (system/project) ثابت بعد الإنشاء.");
}
