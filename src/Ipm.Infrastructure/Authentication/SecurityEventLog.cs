using System.Collections.Concurrent;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Authentication;

/// <summary>
/// Singleton writer of <see cref="SecurityEvent"/> rows, each in its own scope and transaction.
/// Failed sign-ins for unknown names are attacker-controlled input: each name is written at most once a minute and at
/// most 1000 names are tracked. Every failure still counts toward the system-wide limit.
/// </summary>
internal sealed class SecurityEventLog(IServiceScopeFactory scopes, TimeProvider time, IHttpContextAccessor http) : ISecurityEventLog
{
    private const int MaxTrackedNames = 1000;
    private const int GlobalFailureLimit = 100;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UnknownNameInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _unknownNames = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<DateTimeOffset> _failures = new();

    public bool IsLoginBlockedGlobally
    {
        get
        {
            Trim(time.GetUtcNow());
            return _failures.Count >= GlobalFailureLimit;
        }
    }

    public async Task LogAsync(
        SecurityEventType type, Guid? userId, string? username, string? reason = null, string? permission = null,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();

        if (type == SecurityEventType.LoginFailed)
        {
            _failures.Enqueue(now);
            if (userId is null && username is not null && !ShouldWriteUnknown(username, now))
            {
                return;
            }
        }

        var ip = http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.SecurityEvents.Add(SecurityEvent.Create(now, type, userId, username, ip, reason, permission));
        await db.SaveChangesAsync(cancellationToken);
    }

    private bool ShouldWriteUnknown(string username, DateTimeOffset now)
    {
        var key = username.ToLowerInvariant();
        if (_unknownNames.TryGetValue(key, out var last) && now - last < UnknownNameInterval)
        {
            return false;
        }

        if (_unknownNames.Count >= MaxTrackedNames)
        {
            var oldest = _unknownNames.MinBy(p => p.Value).Key;
            _unknownNames.TryRemove(oldest, out _);
        }

        _unknownNames[key] = now;
        return true;
    }

    private void Trim(DateTimeOffset now)
    {
        while (_failures.TryPeek(out var oldest) && now - oldest > Window)
        {
            _failures.TryDequeue(out _);
        }
    }
}
