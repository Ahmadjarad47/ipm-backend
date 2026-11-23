using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Repositories.Commands.SyncRepository;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Repositories.SyncRepository;

internal sealed class SyncRepositoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{projectId:guid}/git-sync", async (Guid projectId, SyncRepositoryRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SyncRepositoryCommand(projectId, r.RepositoryId), ct)).ToHttpResult())
            .WithTags(GitTags.Repositories)
            .WithSummary("مزامنة المستودع")
            .WithDescription("بصلاحية git.sync. بيقرأ آخر 50 كوميت من الفرع الافتراضي ويربطها. فشل خادم Git ← 502 وبيتسجّل بآخر مزامنة.");
}
