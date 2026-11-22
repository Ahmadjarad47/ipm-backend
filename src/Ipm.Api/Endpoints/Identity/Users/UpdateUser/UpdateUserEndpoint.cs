using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.UpdateUser;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.UpdateUser;

internal sealed class UpdateUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("users/{id:guid}", async (Guid id, UpdateUserRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new UpdateUserCommand(id, r.Name, r.Email, r.Title ?? string.Empty, r.ManagerId, version), ct))
                    .ToETagResult(http.Response, u => u.Version);
            })
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("تعديل بيانات مستخدم (If-Match)")
            .WithDescription("الاسم والبريد والمسمى والمدير المباشر. الأدوار لها رابط خاص. المدير لازم يكون نشط وبدون حلقة دائرية.");
}
