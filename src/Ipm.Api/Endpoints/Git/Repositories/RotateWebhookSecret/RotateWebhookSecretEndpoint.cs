using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Repositories.Commands.RotateWebhookSecret;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Repositories.RotateWebhookSecret;

internal sealed class RotateWebhookSecretEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{projectId:guid}/repositories/{id:guid}/rotate-secret", async (Guid projectId, Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RotateWebhookSecretCommand(projectId, id), ct)).ToHttpResult())
            .WithTags(GitTags.Repositories)
            .WithSummary("تدوير سرّ الويب هوك")
            .WithDescription("بصلاحية git.manage. بيولّد سرّاً جديداً ويرجعه مرة وحدة؛ القديم بيبطل فوراً.");
}
