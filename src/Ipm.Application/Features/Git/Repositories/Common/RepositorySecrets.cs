using System.Security.Cryptography;

namespace Ipm.Application.Features.Git.Repositories.Common;

internal static class RepositorySecrets
{
    public const int MinSecretLength = 16;

    /// <summary>A new random webhook secret (48 hex characters).</summary>
    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
}
