using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.GitEvents.Commands.UnlinkGitEvent;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Events.UnlinkGitEvent;

internal sealed class UnlinkGitEventEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("git-events/{id:guid}/unlink", async (Guid id, UnlinkGitEventRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UnlinkGitEventCommand(id, r.Ref), ct)).ToHttpResult())
            .WithTags(GitTags.Events)
            .WithSummary("فك ربط حدث عن مهمة")
            .WithDescription("بصلاحية tasks.edit. بيشتغل على الروابط اليدوية والتلقائية.");
}
