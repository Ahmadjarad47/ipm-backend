using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.GitEvents.Queries.GetGitEvents;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Events.GetGitEvents;

internal sealed class GetGitEventsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("git-events", async (
                    ISender sender,
                    CancellationToken ct,
                    Guid? projectId = null,
                    string? workItemRef = null,
                    string? kind = null,
                    string? search = null,
                    int page = 1,
                    int pageSize = 50) =>
                (await sender.Send(new GetGitEventsQuery(projectId, workItemRef, kind, search, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(GitTags.Events)
            .WithSummary("أحداث Git (كوميتات وطلبات دمج)")
            .WithDescription("بصلاحية git.view بالمشاريع اللي بتشوفها. kind: push أو pull-request أو merge. workItemRef مثل ERP-104. search بالرسالة أو الفرع أو الكاتب.");
}
