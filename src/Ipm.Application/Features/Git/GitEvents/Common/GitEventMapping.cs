using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.GitEvents.Common;

internal static class GitEventMapping
{
    public static GitEventResponse ToResponse(this GitEvent e) => new(
        e.Id, e.ProjectId, e.RepositoryId, e.Kind, e.Title, e.Branch, e.Sha, e.Author, e.Url, e.WorkItemRefs, e.ManualRefs, e.PrNumber, e.PrState, e.DeliveryId, e.CreatedAt, e.Version,
        e.OccurredAt ?? e.CreatedAt);
}
