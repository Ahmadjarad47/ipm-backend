using Ipm.Api.Endpoints.Identity.Auth.Common;
using Ipm.Application.Features.Identity.Auth.Commands.ChangeMyPassword;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Auth.ChangeMyPassword;

internal sealed class ChangeMyPasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/password", async (ChangeMyPasswordRequest request, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword), ct)).ToSessionResult(response))
            .WithTags(IdentityTags.Auth)
            .WithSummary("تغيير كلمة المرور")
            .WithDescription("ينهي باقي الجلسات ويرجع توكنات جديدة للجلسة الحالية، ويصفّر mustChangePassword.");
}
