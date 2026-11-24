using System.Net.Http.Headers;
using System.Text.Json;
using Ipm.Application.Abstractions.Git;
using Ipm.Domain.Common;
using Ipm.Domain.Git;

namespace Ipm.Infrastructure.Git;

/// <summary>
/// Reads the latest commits through the REST API of Gitea, GitHub or GitLab (R10). The URL is checked again with the same SSRF policy as
/// connecting, a call has a timeout, and the token travels only in the request header.
/// </summary>
internal sealed class GitRemote(HttpClient client) : IGitRemote
{
    public async Task<Result<IReadOnlyList<RemoteCommit>>> RecentCommitsAsync(
        GitProvider provider, string repositoryUrl, string name, string branch, string accessToken, int limit, CancellationToken cancellationToken)
    {
        if (RepositoryUrlPolicy.Validate(repositoryUrl) is { } problem)
        {
            return Error.Failure("Git.UrlNotAllowed", problem);
        }

        var origin = new Uri(repositoryUrl).GetLeftPart(UriPartial.Authority);
        var (url, header) = provider switch
        {
            GitProvider.Github => (
                $"{(new Uri(repositoryUrl).Host == "github.com" ? "https://api.github.com" : origin + "/api/v3")}/repos/{name}/commits?sha={Uri.EscapeDataString(branch)}&per_page={limit}",
                ("Authorization", $"Bearer {accessToken}")),
            GitProvider.Gitlab => (
                $"{origin}/api/v4/projects/{Uri.EscapeDataString(name)}/repository/commits?ref_name={Uri.EscapeDataString(branch)}&per_page={limit}",
                ("PRIVATE-TOKEN", accessToken)),
            _ => (
                $"{origin}/api/v1/repos/{name}/commits?sha={Uri.EscapeDataString(branch)}&limit={limit}",
                ("Authorization", $"token {accessToken}")),
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation(header.Item1, header.Item2);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ipm", "1.0"));
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Error.BadGateway("Git.RemoteFailed", $"رفض خادم Git الطلب ({(int)response.StatusCode}).");
            }

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().Select(c => Read(provider, c)).Where(c => c.Sha.Length > 0).Take(limit).ToList()
                : Error.BadGateway("Git.RemoteFailed", "ردّ خادم Git غير متوقع.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            return Error.BadGateway("Git.RemoteFailed", e is TaskCanceledException or OperationCanceledException ? "تعذّر الاتصال بخادم Git: انتهت المهلة." : "تعذّر الاتصال بخادم Git.");
        }
    }

    private static RemoteCommit Read(GitProvider provider, JsonElement commit)
    {
        string Text(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

        if (provider == GitProvider.Gitlab)
        {
            return new RemoteCommit(
                Text(commit, "id"), Text(commit, "message"), Text(commit, "author_name"), Text(commit, "web_url"),
                GitTime.Parse(Text(commit, "authored_date")) ?? GitTime.Parse(Text(commit, "committed_date")) ?? GitTime.Parse(Text(commit, "created_at")));
        }

        var inner = commit.TryGetProperty("commit", out var c) ? c : default;
        var authorNode = inner.ValueKind == JsonValueKind.Object && inner.TryGetProperty("author", out var a) ? a : default;
        var committerNode = inner.ValueKind == JsonValueKind.Object && inner.TryGetProperty("committer", out var m) ? m : default;
        var when = GitTime.Parse(Text(authorNode, "date")) ?? GitTime.Parse(Text(committerNode, "date")) ?? GitTime.Parse(Text(commit, "created"));
        return new RemoteCommit(Text(commit, "sha"), Text(inner, "message"), Text(authorNode, "name"), Text(commit, "html_url"), when);
    }
}
