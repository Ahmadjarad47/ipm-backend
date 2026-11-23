using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Git;

/// <summary>Helpers for part 07: connecting a repository and sending signed webhook calls the way a Git server does.</summary>
internal static class GitApi
{
    public const string Secret = "unit-test-webhook-secret-123456";

    public static async Task<(Guid Id, JsonElement Body)> ConnectAsync(
        HttpClient admin, Guid project, string provider = "gitea", string? name = null, string? secret = Secret)
    {
        var response = await admin.PostAsJsonAsync(
            new Uri($"/api/v1/projects/{project}/repositories", UriKind.Relative),
            new { provider, name = name ?? $"acme/{Unique("repo")}".ToLowerInvariant(), url = "https://git.example.com/acme/app", defaultBranch = "main", accessToken = "tok-abc-123", webhookSecret = secret });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        return (body.GetProperty("id").GetGuid(), body);
    }

    public static string Stamp(int secondsAgo = 0) => DateTimeOffset.UtcNow.AddSeconds(-secondsAgo).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Signs <paramref name="body"/> as the given provider would and posts it to the anonymous webhook endpoint.</summary>
    public static async Task<HttpResponseMessage> DeliverAsync(
        ApiFactory factory, string provider, Guid repository, string body, string? deliveryId = null, string eventName = "push",
        string secret = Secret, string? timestamp = null, string? signature = null, bool omitDelivery = false)
    {
        var client = factory.CreateClient();
        var stamp = timestamp ?? Stamp();
        var hex = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{stamp}.{body}")));
        var id = deliveryId ?? Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/webhooks/{provider}/{repository}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("X-Delivery-Timestamp", stamp);
        switch (provider)
        {
            case "github":
                request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", signature ?? $"sha256={hex}");
                request.Headers.TryAddWithoutValidation("X-GitHub-Event", eventName);
                if (!omitDelivery)
                {
                    request.Headers.TryAddWithoutValidation("X-GitHub-Delivery", id);
                }

                break;
            case "gitlab":
                request.Headers.TryAddWithoutValidation("X-Gitlab-Token", signature ?? secret);
                request.Headers.TryAddWithoutValidation("X-Gitlab-Event", eventName == "push" ? "Push Hook" : "Merge Request Hook");
                if (!omitDelivery)
                {
                    request.Headers.TryAddWithoutValidation("X-Gitlab-Event-UUID", id);
                }

                break;
            default:
                request.Headers.TryAddWithoutValidation("X-Gitea-Signature", signature ?? hex);
                request.Headers.TryAddWithoutValidation("X-Gitea-Event", eventName);
                if (!omitDelivery)
                {
                    request.Headers.TryAddWithoutValidation("X-Gitea-Delivery", id);
                }

                break;
        }

        return await client.SendAsync(request);
    }

    public static string PushBody(string message, string branch = "main", string? sha = null) =>
        JsonSerializer.Serialize(new
        {
            @ref = $"refs/heads/{branch}",
            commits = new[] { new { id = sha ?? Guid.NewGuid().ToString("N") + "00000000", message, url = "https://git.example.com/acme/app/commit/x", author = new { username = "dev", name = "Dev" } } },
        });

    public static string PrBody(int number, string title, string action = "opened", bool merged = false, string branch = "feature/x") =>
        JsonSerializer.Serialize(new
        {
            action,
            pull_request = new { number, title, merged, html_url = "https://git.example.com/acme/app/pulls/1", head = new { @ref = branch, sha = "abc123abc123" }, user = new { login = "dev" } },
        });

    public static async Task<JsonElement> EventsAsync(HttpClient client, Guid project, string? workItemRef = null, string? kind = null)
    {
        var url = $"/api/v1/git-events?projectId={project}" + (workItemRef is null ? string.Empty : $"&workItemRef={workItemRef}") + (kind is null ? string.Empty : $"&kind={kind}");
        return (await GetAsync(client, url)).Body;
    }

    public static async Task<JsonElement> DeliveriesAsync(HttpClient client, Guid project) =>
        (await GetAsync(client, $"/api/v1/git/deliveries?projectId={project}")).Body;
}
