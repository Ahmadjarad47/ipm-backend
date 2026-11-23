using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Repositories.Queries.GetRepositories;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Repositories.GetRepositories;

internal sealed class GetRepositoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("projects/{projectId:guid}/repositories", async (Guid projectId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetRepositoriesQuery(projectId), ct)).ToHttpResult())
            .WithTags(GitTags.Repositories)
            .WithSummary("مستودعات المشروع")
            .WithDescription("بصلاحية git.view. ما بيرجع التوكن ولا سرّ الويب هوك أبداً، فقط webhookUrl وhasCredential.");
}
