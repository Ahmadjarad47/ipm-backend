using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.ResetUserPassword;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.ResetUserPassword;

internal sealed class ResetUserPasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("users/{id:guid}/reset-password", async (Guid id, ResetUserPasswordRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ResetUserPasswordCommand(id, r.NewPassword), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("إعادة تعيين كلمة مرور مستخدم")
            .WithDescription("كلمة مؤقتة يحطها المدير؛ بتقطع جلسات المستخدم وبتصفّر القفل، والمستخدم مجبور يغيّرها عند الدخول.");
}
