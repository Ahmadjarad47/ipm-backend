using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Repositories.Commands.DisconnectRepository;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Repositories.DisconnectRepository;

internal sealed class DisconnectRepositoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{projectId:guid}/repositories/{id:guid}/disconnect", async (Guid projectId, Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DisconnectRepositoryCommand(projectId, id), ct)).ToHttpResult())
            .WithTags(GitTags.Repositories)
            .WithSummary("فصل مستودع")
            .WithDescription("بصلاحية git.manage. بيبقى السجل والأحداث، بيتمسح التوكن والسرّ، وأي تسليم بعدها بيرجع 422.");
}
