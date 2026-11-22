namespace Ipm.Domain.Identity;

/// <summary>
/// One security-relevant event: a sign-in attempt, a refused permission, a password change. Append only
/// (never updated or deleted), so it is not an <c>Entity</c> and not in <c>audit_log</c> (it is not a change of data).
/// Part 08 shows these in the activity and audit screen.
/// </summary>
public sealed class SecurityEvent
{
    public const int UsernameMaxLength = 64;
    public const int ReasonMaxLength = 64;

    private SecurityEvent() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public DateTimeOffset At { get; private init; }

    public SecurityEventType Type { get; private init; }

    public Guid? UserId { get; private init; }

    public string? Username { get; private init; }

    public string? Ip { get; private init; }

    public string? Reason { get; private init; }

    public string? Permission { get; private init; }

    public static SecurityEvent Create(
        DateTimeOffset at, SecurityEventType type, Guid? userId, string? username, string? ip, string? reason = null, string? permission = null) =>
        new()
        {
            At = at,
            Type = type,
            UserId = userId,
            Username = username is null ? null : Truncate(username, UsernameMaxLength),
            Ip = ip,
            Reason = reason is null ? null : Truncate(reason, ReasonMaxLength),
            Permission = permission,
        };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
