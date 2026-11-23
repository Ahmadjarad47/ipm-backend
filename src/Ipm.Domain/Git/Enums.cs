namespace Ipm.Domain.Git;

/// <summary>The Git providers whose webhooks and APIs are understood. Shown as gitea, github, gitlab.</summary>
public enum GitProvider
{
    Gitea = 0,
    Github = 1,
    Gitlab = 2,
}

/// <summary>What a Git event is: a pushed commit, an open or closed pull request, or a merged one. Shown as push, pull-request, merge.</summary>
public enum GitEventKind
{
    Push = 0,
    PullRequest = 1,
    Merge = 2,
}

public enum PullRequestState
{
    Open = 0,
    Merged = 1,
    Closed = 2,
}

/// <summary>What became of one webhook call (R5, R6): handled, ignored as a repeat, refused, or failed while handling.</summary>
public enum DeliveryOutcome
{
    Processed = 0,
    Duplicate = 1,
    Rejected = 2,
    Failed = 3,
}
