using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Git.Repositories.Commands.ConnectRepository;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Repositories.ConnectRepository;

internal sealed class ConnectRepositoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{projectId:guid}/repositories", async (Guid projectId, ConnectRepositoryRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ConnectRepositoryCommand(projectId, r.Provider, r.Name, r.Url, r.DefaultBranch, r.AccessToken, r.WebhookSecret), ct))
                .ToCreatedResult(c => $"/api/v1/projects/{projectId}/repositories/{c.Id}"))
            .WithTags(GitTags.Repositories)
            .WithSummary("ربط مستودع بالمشروع")
            .WithDescription("بصلاحية git.manage. الرابط https فقط وبدون عناوين داخلية. التوكن والسرّ بينخزنوا مشفّرين. webhookSecret اختياري (16 حرف وأكثر) وإذا انحذف بنولّد سرّاً عشوائياً؛ السرّ بيرجع مرة وحدة بهالرد فقط.");
}
