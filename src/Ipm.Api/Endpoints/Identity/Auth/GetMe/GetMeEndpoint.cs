using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Auth.Queries.GetMe;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Auth.GetMe;

internal sealed class GetMeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("auth/me", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMeQuery(), ct)).ToHttpResult())
            .WithTags(IdentityTags.Auth)
            .WithSummary("المستخدم الحالي وصلاحياته")
            .WithDescription("اسم المستخدم + صلاحيات النظام + الممنوع + هل لازم يغيّر كلمة المرور. صلاحيات المشاريع بتنضاف مع الجزء 04.");
}
