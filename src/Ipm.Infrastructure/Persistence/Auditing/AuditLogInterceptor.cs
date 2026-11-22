using System.Text.Json;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ipm.Infrastructure.Persistence.Auditing;

/// <summary>
/// Adds an <see cref="AuditLogEntry"/> for every inserted, updated or deleted <see cref="IAuditable"/> record,
/// inside the same SaveChanges (so the same transaction). Must run after <c>AuditableEntityInterceptor</c>
/// so the stamps are part of "after".
/// Insert/delete store every column; update stores only the columns that changed.
/// </summary>
public sealed class AuditLogInterceptor(IUserContext userContext, TimeProvider timeProvider) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Write(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Write(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Write(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        var userId = userContext.UserId;

        var entries = context.ChangeTracker.Entries<IAuditable>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => Describe(e, now, userId))
            .OfType<AuditLogEntry>()
            .ToList();

        context.Set<AuditLogEntry>().AddRange(entries);
    }

    private static AuditLogEntry? Describe(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        // Works for Entity and for Employee (an Identity user): both have a single Guid key.
        var key = entry.Metadata.FindPrimaryKey()?.Properties;
        if (key is not [var keyProperty] || entry.Property(keyProperty.Name).CurrentValue is not Guid entityId)
        {
            return null;
        }

        var properties = entry.Properties
            .Where(p => !p.Metadata.IsExcludedFromAudit() && !p.Metadata.IsConcurrencyToken)
            .ToList();

        var (action, before, after) = entry.State switch
        {
            EntityState.Added => (AuditAction.Insert, null, Snapshot(properties, p => p.CurrentValue)),
            EntityState.Deleted => (AuditAction.Delete, Snapshot(properties, p => p.OriginalValue), null),
            _ => Changed(properties),
        };

        // An update that only touched excluded columns leaves nothing to record.
        if (action == AuditAction.Update && after is null)
        {
            return null;
        }

        var table = entry.Metadata.GetTableName();
        var schema = entry.Metadata.GetSchema();

        return new AuditLogEntry
        {
            Entity = schema is null ? table! : $"{schema}.{table}",
            EntityId = entityId,
            Action = action,
            Before = before,
            After = after,
            UserId = userId,
            At = now,
        };
    }

    private static (AuditAction, string?, string?) Changed(List<PropertyEntry> properties)
    {
        var modified = properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
        return modified.Count == 0
            ? (AuditAction.Update, null, null)
            : (AuditAction.Update, Snapshot(modified, p => p.OriginalValue), Snapshot(modified, p => p.CurrentValue));
    }

    private static string Snapshot(IEnumerable<PropertyEntry> properties, Func<PropertyEntry, object?> value) =>
        JsonSerializer.Serialize(
            properties.ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Metadata.Name), value),
            Json);
}
