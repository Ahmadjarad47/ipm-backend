using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Queries.GetUserDirectory;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.GetUserDirectory;

internal sealed class GetUserDirectoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("users/directory", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetUserDirectoryQuery(), ct)).ToHttpResult())
            .WithTags(IdentityTags.Users)
            .WithSummary("دليل المستخدمين المختصر (للاختيار بالقوائم)")
            .WithDescription("الاسم واسم المستخدم والمسمى والحالة فقط، بدون بريد أو مدير أو أدوار.");
}
