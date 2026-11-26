using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.UnlockUser;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.UnlockUser;

internal sealed class UnlockUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("users/{id:guid}/unlock", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UnlockUserCommand(id), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("فك قفل حساب مستخدم")
            .WithDescription("بيفك القفل الناتج عن محاولات الدخول الفاشلة فورًا ويصفّر عدّاد المحاولات، بدون تغيير كلمة المرور.");
}
