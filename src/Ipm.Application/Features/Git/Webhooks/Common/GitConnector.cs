using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Webhooks.Common;

/// <summary>
/// One per Git provider (R5): each names its headers and signs differently, so the check, the event names and the URLs are never shared
/// through a default. A delivery is signed over <c>"&lt;timestamp&gt;.&lt;rawBody&gt;"</c> so the timestamp cannot be swapped to dodge the replay window.
/// </summary>
internal sealed class GitConnector
{
    public const string TimestampHeader = "X-Delivery-Timestamp";
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    private static readonly GitConnector Gitea = new("X-Gitea-Signature", "X-Gitea-Event", "X-Gitea-Delivery", hmac: true, prefix: false, "/commit/", "/pulls/");
    private static readonly GitConnector Github = new("X-Hub-Signature-256", "X-GitHub-Event", "X-GitHub-Delivery", hmac: true, prefix: true, "/commit/", "/pull/");
    private static readonly GitConnector Gitlab = new("X-Gitlab-Token", "X-Gitlab-Event", "X-Gitlab-Event-UUID", hmac: false, prefix: false, "/-/commit/", "/-/merge_requests/");

    private readonly bool _hmac;
    private readonly bool _prefix;

    private GitConnector(string signatureHeader, string eventHeader, string deliveryHeader, bool hmac, bool prefix, string commitPath, string prPath)
    {
        SignatureHeader = signatureHeader;
        EventHeader = eventHeader;
        DeliveryHeader = deliveryHeader;
        CommitPath = commitPath;
        PrPath = prPath;
        _hmac = hmac;
        _prefix = prefix;
    }

    public string SignatureHeader { get; }

    public string EventHeader { get; }

    public string DeliveryHeader { get; }

    public string CommitPath { get; }

    public string PrPath { get; }

    public static GitConnector For(GitProvider provider) => provider switch
    {
        GitProvider.Github => Github,
        GitProvider.Gitlab => Gitlab,
        _ => Gitea,
    };

    /// <summary>The internal event name: <c>push</c> or <c>pull_request</c>. GitLab calls them "Push Hook" and "Merge Request Hook".</summary>
    public static string NormalizeEvent(string raw) => raw switch
    {
        "Push Hook" => "push",
        "Merge Request Hook" => "pull_request",
        _ => raw,
    };

    /// <summary>The value the signature header must carry (used by tests and the demo to build a valid call).</summary>
    public string Sign(string secret, string raw, string timestamp)
    {
        if (!_hmac)
        {
            return secret; // GitLab sends the shared token itself
        }

        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{raw}")));
        return _prefix ? $"sha256={hash}" : hash;
    }

    /// <summary>Constant-time check of the delivery. A missing signature or timestamp fails.</summary>
    public bool Verify(string secret, string raw, string? signature, string? timestamp)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(secret, raw, timestamp));
        var given = Encoding.UTF8.GetBytes(_hmac && !_prefix ? signature.Replace("sha256=", string.Empty, StringComparison.Ordinal) : signature);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>R5: ISO date, epoch seconds or epoch milliseconds; null when unreadable.</summary>
    public static DateTimeOffset? ParseTimestamp(string value)
    {
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return number < 1_000_000_000_000 ? DateTimeOffset.FromUnixTimeSeconds(number) : DateTimeOffset.FromUnixTimeMilliseconds(number);
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;
    }

    public string CommitUrl(string repositoryUrl, string sha) => $"{repositoryUrl.TrimEnd('/')}{CommitPath}{sha}";

    public string PrUrl(string repositoryUrl, int number) => $"{repositoryUrl.TrimEnd('/')}{PrPath}{number}";
}
