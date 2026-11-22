using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.CreateUser;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.CreateUser;

internal sealed class CreateUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("users", async (CreateUserRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateUserCommand(r.Username, r.Name, r.Email, r.Title ?? string.Empty, r.ManagerId, r.SystemRoleIds ?? [], r.Password), ct))
                    .ToCreatedETagResult(response, u => u.Version, u => $"/api/v1/users/{u.Id}"))
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("إنشاء مستخدم")
            .WithDescription("المدير بيحط كلمة المرور الأولى والمستخدم مجبور يغيّرها عند أول دخول. الأدوار لازم تكون من نطاق system، وما بتسند دور فيه صلاحية ما بتملكها.");
}
