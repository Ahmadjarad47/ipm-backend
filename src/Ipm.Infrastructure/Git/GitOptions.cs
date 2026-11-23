using System.ComponentModel.DataAnnotations;

namespace Ipm.Infrastructure.Git;

/// <summary>Section <c>Git</c>. In production the key comes from <c>Git__SecretKey</c>: 32 random bytes in base64.</summary>
public sealed class GitOptions
{
    public const string Section = "Git";

    /// <summary>The AES-256 key that encrypts Git access tokens and webhook secrets (base64, 32 bytes). The service does not start without a valid one.</summary>
    [Required]
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Seconds a call to a Git server may take before the sync gives up.</summary>
    [Range(1, 120)]
    public int RemoteTimeoutSeconds { get; init; } = 10;
}
