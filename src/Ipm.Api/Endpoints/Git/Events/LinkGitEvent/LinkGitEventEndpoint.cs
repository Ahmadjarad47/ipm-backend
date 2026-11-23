using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.GitEvents.Commands.LinkGitEvent;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Events.LinkGitEvent;

internal sealed class LinkGitEventEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("git-events/{id:guid}/link", async (Guid id, LinkGitEventRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new LinkGitEventCommand(id, r.Ref), ct)).ToHttpResult())
            .WithTags(GitTags.Events)
            .WithSummary("ربط يدوي لحدث بمهمة")
            .WithDescription("بصلاحية tasks.edit. المهمة لازم من نفس مشروع الحدث. مربوط مسبقاً ← 409.");
}
