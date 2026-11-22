using Ipm.Domain.Identity;

namespace Ipm.Application.Abstractions.Authentication;

/// <summary>
/// Writes <see cref="SecurityEvent"/> rows (R21) in their own transaction, so a failed request cannot lose them
/// and logging never commits half-finished work of the request.
/// </summary>
public interface ISecurityEventLog
{
    /// <summary>The client address and time are taken from the current request. A failed sign-in for an unknown name is written at most once a minute per name.</summary>
    Task LogAsync(
        SecurityEventType type,
        Guid? userId,
        string? username,
        string? reason = null,
        string? permission = null,
        CancellationToken cancellationToken = default);

    /// <summary>True when too many sign-ins failed system-wide in the last 15 minutes (distributed guessing, R2).</summary>
    bool IsLoginBlockedGlobally { get; }
}
