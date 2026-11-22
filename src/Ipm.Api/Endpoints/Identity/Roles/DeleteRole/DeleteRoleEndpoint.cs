using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Roles.Commands.DeleteRole;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Roles.DeleteRole;

internal sealed class DeleteRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("roles/{id:guid}", async (Guid id, ISender sender, HttpRequest request, CancellationToken ct) =>
            {
                if (request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new DeleteRoleCommand(id, version), ct)).ToHttpResult();
            })
            .RequireAuthorization(Permissions.RolesManage)
            .WithTags(IdentityTags.Roles)
            .WithSummary("حذف دور (If-Match)")
            .WithDescription("الأدوار المدمجة ما بتنحذف (422)، والدور المسند لمستخدمين ما بينحذف (409).");
}
