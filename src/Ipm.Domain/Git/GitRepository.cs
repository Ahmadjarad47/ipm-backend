using System.Text.RegularExpressions;
using Ipm.Domain.Common;

namespace Ipm.Domain.Git;

/// <summary>
/// A Git repository linked to a project (FR-GIT-01). The access token and the webhook secret are stored encrypted and never returned or
/// audited (R15). Disconnecting keeps the record and its events but wipes both secrets, so it can no longer authenticate deliveries (R2).
/// </summary>
public sealed partial class GitRepository : AuditableEntity, IAuditSubject
{
    Guid? IAuditSubject.AuditProjectId => ProjectId;

    string? IAuditSubject.AuditLabel => Name;


    public const int NameMaxLength = 200;

    private GitRepository() { }

    public Guid ProjectId { get; private set; }

    public GitProvider Provider { get; private set; }

    /// <summary><c>owner/repository</c>.</summary>
    public string Name { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public string DefaultBranch { get; private set; } = string.Empty;

    public bool IsConnected { get; private set; }

    public string? AccessTokenCipher { get; private set; }

    public string? WebhookSecretCipher { get; private set; }

    public DateTimeOffset? LastSyncAt { get; private set; }

    public string? LastSyncError { get; private set; }

    public bool HasCredential => AccessTokenCipher is not null;

    public static bool IsValidName(string name) => NamePattern().IsMatch(name);

    /// <summary>R1: the secrets arrive already encrypted. The URL and the other rules are checked by the handler.</summary>
    public static GitRepository Connect(
        Guid projectId, GitProvider provider, string name, string url, string defaultBranch, string accessTokenCipher, string webhookSecretCipher, Guid? id = null)
    {
        var repository = id is { } fixedId ? new GitRepository(fixedId) : new GitRepository();
        repository.ProjectId = projectId;
        repository.Provider = provider;
        repository.Name = name.Trim();
        repository.Url = url.Trim();
        repository.DefaultBranch = defaultBranch.Trim();
        repository.IsConnected = true;
        repository.AccessTokenCipher = accessTokenCipher;
        repository.WebhookSecretCipher = webhookSecretCipher;
        return repository;
    }

    private GitRepository(Guid id) : base(id) { }

    /// <summary>R2: keeps the record, drops the connection. A disconnected repository is not synced, so its last sync error no longer applies.</summary>
    public void Disconnect()
    {
        IsConnected = false;
        AccessTokenCipher = null;
        WebhookSecretCipher = null;
        LastSyncError = null;
    }

    /// <summary>
    /// R2: connecting a repository again reuses its disconnected record, so its events and deliveries stay with it instead of being
    /// left on a second, dead record. The secrets arrive already encrypted.
    /// </summary>
    public Result Reconnect(GitProvider provider, string url, string defaultBranch, string accessTokenCipher, string webhookSecretCipher)
    {
        if (IsConnected)
        {
            return GitErrors.AlreadyConnected;
        }

        Provider = provider;
        Url = url.Trim();
        DefaultBranch = defaultBranch.Trim();
        IsConnected = true;
        AccessTokenCipher = accessTokenCipher;
        WebhookSecretCipher = webhookSecretCipher;
        LastSyncError = null;
        return Result.Success();
    }

    /// <summary>R3.</summary>
    public Result RotateSecret(string webhookSecretCipher)
    {
        if (!IsConnected)
        {
            return GitErrors.Disconnected;
        }

        WebhookSecretCipher = webhookSecretCipher;
        return Result.Success();
    }

    /// <summary>R10: the last successful sync, clearing the last error.</summary>
    public void RecordSync(DateTimeOffset at)
    {
        LastSyncAt = at;
        LastSyncError = null;
    }

    /// <summary>R10.</summary>
    public void RecordSyncError(string message) => LastSyncError = message.Length > 500 ? message[..500] : message;

    [GeneratedRegex(@"^[\w.-]+/[\w.-]+$")]
    private static partial Regex NamePattern();
}
