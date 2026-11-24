using System.Text.Json;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Git;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Webhooks.Common;

/// <summary>R7 and R8: turns a verified webhook payload (or a synced commit) into Git events and links them to work items. It never moves or approves an item.</summary>
internal static class GitIngestion
{
    /// <summary>Applies the payload; returns the refs it linked. Idempotent per commit sha and per pull request number. Throws <see cref="JsonException"/> for a body that is not JSON.</summary>
    public static async Task<Result<IReadOnlyList<string>>> ProcessAsync(
        IApplicationDbContext db, GitRepository repository, Project project, string eventName, string raw, string deliveryId, CancellationToken cancellationToken)
    {
        if (!repository.IsConnected)
        {
            return GitErrors.Disconnected;
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var linked = new List<string>();

        if (eventName == "push")
        {
            var branch = Text(root, "ref").Replace("refs/heads/", string.Empty, StringComparison.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
            {
                foreach (var commit in commits.EnumerateArray())
                {
                    var author = commit.TryGetProperty("author", out var a) ? Text(a, "username", Text(a, "name")) : string.Empty;
                    var (_, refs) = await IngestCommitAsync(
                        db, repository, project, branch, Text(commit, "id"), Text(commit, "message"), author, Text(commit, "url"), deliveryId, seen, cancellationToken,
                        GitTime.Parse(Text(commit, "timestamp")));
                    linked.AddRange(refs.Where(r => !linked.Contains(r)));
                }
            }
        }
        else if (eventName == "pull_request")
        {
            linked.AddRange(await IngestPullRequestAsync(db, repository, project, root, deliveryId, cancellationToken));
        }

        return linked;
    }

    /// <summary>
    /// Stores one commit once per repository (a repeat is ignored) with the refs of the project items found in its message and branch.
    /// Returns the refs linked, or an empty list when the commit was already known. A known commit stored without its time gets it filled in.
    /// </summary>
    public static async Task<(bool Added, IReadOnlyList<string> Refs)> IngestCommitAsync(
        IApplicationDbContext db, GitRepository repository, Project project, string branch, string sha, string message, string author, string url, string deliveryId,
        HashSet<string> seen, CancellationToken cancellationToken, DateTimeOffset? committedAt = null)
    {
        if (string.IsNullOrWhiteSpace(sha) || !seen.Add(sha))
        {
            return (false, []);
        }

        var known = await db.GitEvents.SingleOrDefaultAsync(e => e.RepositoryId == repository.Id && e.Sha == sha && e.Kind == GitEventKind.Push, cancellationToken);
        if (known is not null)
        {
            if (committedAt is { } time && known.OccurredAt is null)
            {
                known.RecordOccurredAt(time);
            }

            return (false, []);
        }

        var refs = await DetectRefsAsync(db, project, cancellationToken, message, branch);
        db.GitEvents.Add(GitEvent.Push(project.Id, repository.Id, message, branch, sha, author, url, refs, deliveryId, committedAt));
        return (true, refs);
    }

    private static async Task<IReadOnlyList<string>> IngestPullRequestAsync(
        IApplicationDbContext db, GitRepository repository, Project project, JsonElement root, string deliveryId, CancellationToken cancellationToken)
    {
        var pr = ReadPullRequest(root);
        if (pr is null)
        {
            return [];
        }

        var refs = await DetectRefsAsync(db, project, cancellationToken, pr.Title, pr.Branch);
        var existing = await db.GitEvents.SingleOrDefaultAsync(e => e.RepositoryId == repository.Id && e.PrNumber == pr.Number, cancellationToken);
        if (existing is null)
        {
            db.GitEvents.Add(GitEvent.PullRequest(project.Id, repository.Id, pr.Number, pr.State, pr.Title, pr.Branch, pr.Sha, pr.Author, pr.Url, refs, deliveryId, pr.OccurredAt));
        }
        else
        {
            existing.UpdatePullRequest(pr.State, pr.Title, pr.Branch, pr.Sha, pr.Author, pr.Url, refs, deliveryId, pr.OccurredAt);
        }

        return refs;
    }

    /// <summary>R7: the refs the pattern finds in the texts, that belong to an existing item of the project.</summary>
    public static async Task<IReadOnlyList<string>> DetectRefsAsync(IApplicationDbContext db, Project project, CancellationToken cancellationToken, params string?[] texts)
    {
        var found = GitRefPatterns.Extract(project.RefPattern ?? GitRefPatterns.Default(project.Code), project.Code, texts);
        if (found.Count == 0)
        {
            return [];
        }

        var existing = await db.WorkItems.AsNoTracking().Where(i => i.ProjectId == project.Id && found.Contains(i.Ref)).Select(i => i.Ref).ToListAsync(cancellationToken);
        return [.. found.Where(existing.Contains)];
    }

    /// <summary>A pull request in the shape of Gitea and GitHub (<c>pull_request</c>) or of GitLab (<c>object_attributes</c>).</summary>
    private static ParsedPullRequest? ReadPullRequest(JsonElement root)
    {
        if (root.TryGetProperty("pull_request", out var pr))
        {
            var merged = pr.TryGetProperty("merged", out var m) && m.ValueKind == JsonValueKind.True;
            var closed = Text(root, "action") == "closed";
            var head = pr.TryGetProperty("head", out var h) ? h : default;
            var sha = merged && Text(pr, "merge_commit_sha") is { Length: > 0 } mergeSha ? mergeSha : Text(head, "sha");
            var when = merged ? Text(pr, "merged_at") : closed ? Text(pr, "closed_at") : Text(pr, "created_at");
            return new ParsedPullRequest(
                pr.TryGetProperty("number", out var n) && n.TryGetInt32(out var number) ? number : 0,
                Text(pr, "title"), Text(head, "ref"), sha, pr.TryGetProperty("user", out var u) ? Text(u, "login") : string.Empty, Text(pr, "html_url"),
                merged ? PullRequestState.Merged : closed ? PullRequestState.Closed : PullRequestState.Open,
                GitTime.Parse(when) ?? GitTime.Parse(Text(pr, "updated_at")));
        }

        if (root.TryGetProperty("object_attributes", out var attributes))
        {
            var state = Text(attributes, "state") switch
            {
                "merged" => PullRequestState.Merged,
                "closed" => PullRequestState.Closed,
                _ => PullRequestState.Open,
            };
            return new ParsedPullRequest(
                attributes.TryGetProperty("iid", out var iid) && iid.TryGetInt32(out var number) ? number : 0,
                Text(attributes, "title"), Text(attributes, "source_branch"),
                attributes.TryGetProperty("last_commit", out var last) ? Text(last, "id") : Text(attributes, "merge_commit_sha"),
                root.TryGetProperty("user", out var user) ? Text(user, "username") : string.Empty, Text(attributes, "url"), state,
                GitTime.Parse(Text(attributes, state == PullRequestState.Open ? "created_at" : "updated_at")) ?? GitTime.Parse(Text(attributes, "updated_at")));
        }

        return null;
    }

    private static string Text(JsonElement element, string name, string fallback = "") =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private sealed record ParsedPullRequest(int Number, string Title, string Branch, string Sha, string Author, string Url, PullRequestState State, DateTimeOffset? OccurredAt);
}
