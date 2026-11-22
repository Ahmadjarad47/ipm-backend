using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.SetUserRoles;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.SetUserRoles;

internal sealed class SetUserRolesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("users/{id:guid}/roles", async (Guid id, SetUserRolesRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetUserRolesCommand(id, r.SystemRoleIds ?? [], version), ct))
                    .ToETagResult(http.Response, u => u.Version);
            })
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("تعيين أدوار النظام لمستخدم (If-Match)")
            .WithDescription("القائمة المرسلة هي الأدوار الكاملة. ممنوع تعدّل أدوارك، أو حساب أعلى منك، أو تسند ما لا تملك، أو تسحب آخر مدير أدوار.");
}
