namespace Ipm.Domain.Identity;

/// <summary>Kinds of security event (R21). Stored and shown as kebab-case text.</summary>
public enum SecurityEventType
{
    LoginSucceeded = 0,
    LoginFailed = 1,
    LockedOut = 2,
    PermissionDenied = 3,
    PasswordChanged = 4,
    PasswordReset = 5,
    SessionsRevoked = 6,
}
