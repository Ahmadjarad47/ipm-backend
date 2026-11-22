using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Roles.Commands.UpdateRole;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Roles.UpdateRole;

internal sealed class UpdateRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("roles/{id:guid}", async (Guid id, UpdateRoleRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateRoleCommand(id, r.Name, r.Description ?? string.Empty, r.Permissions ?? [], r.Denied ?? [], r.Scope, version), ct))
                    .ToETagResult(http.Response, role => role.Version);
            })
            .RequireAuthorization(Permissions.RolesManage)
            .WithTags(IdentityTags.Roles)
            .WithSummary("تعديل دور وصلاحياته (If-Match)")
            .WithDescription("الصلاحيات المرسلة هي القائمة الكاملة. ما بتضيف صلاحية ما بتملكها، ولازم يضل مستخدم نشط بصلاحية roles.manage. التعديل بيسري على المستخدمين بالطلب التالي.");
}
