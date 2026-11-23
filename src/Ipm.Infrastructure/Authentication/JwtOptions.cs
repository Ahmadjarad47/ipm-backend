using System.ComponentModel.DataAnnotations;

namespace Ipm.Infrastructure.Authentication;

/// <summary>Section <c>Jwt</c>. In production the signing key comes from <c>Jwt__SigningKey</c>.</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; init; } = "ipm";

    [Required]
    public string Audience { get; init; } = "ipm";

    /// <summary>HMAC-SHA256 key, at least 32 characters.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; init; } = 15;

}
