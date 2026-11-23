using Ipm.Domain.Common;

namespace Ipm.Domain.Git;

/// <summary>
/// A commit or a pull request seen through a webhook or a sync (FR-GIT-02). The refs it mentions are linked automatically (R7); a person
/// can add or remove links by hand (R11), and those survive later updates of the pull request.
/// </summary>
public sealed class GitEvent : AuditableEntity
{
    public const int TitleMaxLength = 500;

    private GitEvent() { }

    private GitEvent(Guid id) : base(id) { }

    public Guid ProjectId { get; private set; }

    public Guid RepositoryId { get; private set; }

    public GitEventKind Kind { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Branch { get; private set; } = string.Empty;

    /// <summary>The full commit sha (up to 40 characters); shown cut to 7.</summary>
    public string Sha { get; private set; } = string.Empty;

    public string Author { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    /// <summary>All linked refs: found by the pattern plus the manual ones.</summary>
    public List<string> WorkItemRefs { get; private set; } = [];

    /// <summary>The part of <see cref="WorkItemRefs"/> linked by hand.</summary>
    public List<string> ManualRefs { get; private set; } = [];

    public int? PrNumber { get; private set; }

    public PullRequestState? PrState { get; private set; }

    public string? DeliveryId { get; private set; }

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static GitEvent Push(
        Guid projectId, Guid repositoryId, string title, string branch, string sha, string author, string url, IEnumerable<string> refs, string? deliveryId, Guid? id = null)
    {
        var gitEvent = id is { } fixedId ? new GitEvent(fixedId) : new GitEvent();
        gitEvent.ProjectId = projectId;
        gitEvent.RepositoryId = repositoryId;
        gitEvent.Kind = GitEventKind.Push;
        gitEvent.Fill(title, branch, sha, author, url, refs, deliveryId);
        return gitEvent;
    }

    public static GitEvent PullRequest(
        Guid projectId, Guid repositoryId, int number, PullRequestState state, string title, string branch, string sha, string author, string url,
        IEnumerable<string> refs, string? deliveryId, Guid? id = null)
    {
        var gitEvent = id is { } fixedId ? new GitEvent(fixedId) : new GitEvent();
        gitEvent.ProjectId = projectId;
        gitEvent.RepositoryId = repositoryId;
        gitEvent.PrNumber = number;
        gitEvent.Fill(title, branch, sha, author, url, refs, deliveryId);
        gitEvent.PrState = state;
        gitEvent.Kind = state == PullRequestState.Merged ? GitEventKind.Merge : GitEventKind.PullRequest;
        return gitEvent;
    }

    /// <summary>R7: a later delivery of the same pull request moves its state and keeps the links made by hand.</summary>
    public void UpdatePullRequest(PullRequestState state, string title, string branch, string sha, string author, string url, IEnumerable<string> refs, string? deliveryId)
    {
        Fill(title, branch, sha, author, url, [.. refs, .. ManualRefs], deliveryId);
        PrState = state;
        Kind = state == PullRequestState.Merged ? GitEventKind.Merge : GitEventKind.PullRequest;
    }

    /// <summary>R11.</summary>
    public Result Link(string reference)
    {
        if (WorkItemRefs.Contains(reference))
        {
            return GitErrors.AlreadyLinked;
        }

        WorkItemRefs = [.. WorkItemRefs, reference];
        ManualRefs = [.. ManualRefs, reference];
        return Result.Success();
    }

    /// <summary>R11: removes the link, automatic or manual.</summary>
    public void Unlink(string reference)
    {
        WorkItemRefs = [.. WorkItemRefs.Where(r => r != reference)];
        ManualRefs = [.. ManualRefs.Where(r => r != reference)];
    }

    private void Fill(string title, string branch, string sha, string author, string url, IEnumerable<string> refs, string? deliveryId)
    {
        var firstLine = title.Split('\n', 2)[0].Trim();
        Title = firstLine.Length > TitleMaxLength ? firstLine[..TitleMaxLength] : firstLine;
        Branch = branch;
        Sha = sha;
        Author = author;
        Url = url;
        WorkItemRefs = [.. refs.Distinct()];
        DeliveryId = deliveryId;
    }
}
