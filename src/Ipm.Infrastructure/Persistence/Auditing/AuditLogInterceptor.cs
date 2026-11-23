using System.Text.Json;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Common;
using Ipm.Domain.Work;
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
public sealed class AuditLogInterceptor(IUserContext userContext, TimeProvider timeProvider, Microsoft.AspNetCore.Http.IHttpContextAccessor http) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            WriteAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await WriteAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task WriteAsync(DbContext context, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userId = userContext.UserId;
        var (source, correlation) = RequestOrigin();

        var tracked = context.ChangeTracker.Entries<IAuditable>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (tracked.Count == 0)
        {
            return;
        }

        var items = await ResolveItemsAsync(context, tracked, cancellationToken);
        var entries = tracked
            .Select(e => Describe(e, now, userId, source, correlation, items))
            .OfType<AuditLogEntry>()
            .ToList();

        context.Set<AuditLogEntry>().AddRange(entries);
    }

    /// <summary>R1: <c>webhook</c> for calls to the Git webhook, <c>ui</c> when the front end says so, <c>api</c> for other calls, <c>system</c> without a request (seed, jobs).</summary>
    private (string Source, string Correlation) RequestOrigin() => OriginOf(http.HttpContext);

    internal static (string Source, string Correlation) OriginOf(Microsoft.AspNetCore.Http.HttpContext? context)
    {
        if (context is null)
        {
            return ("system", string.Empty);
        }

        var source = context.Request.Path.StartsWithSegments("/api/v1/webhooks", StringComparison.OrdinalIgnoreCase) ? "webhook"
            : string.Equals(context.Request.Headers["X-Client"], "ui", StringComparison.OrdinalIgnoreCase) ? "ui"
            : "api";
        var given = context.Request.Headers["X-Correlation-Id"].ToString();
        var correlation = given.Length is > 0 and <= 64 && given.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.') ? given : context.TraceIdentifier;
        return (source, correlation.Length > 64 ? correlation[..64] : correlation);
    }

    /// <summary>The project and ref of the work items that comments, links and approvals hang on (tracked ones first, the rest from the database).</summary>
    private static async Task<Dictionary<Guid, (Guid ProjectId, string Ref)>> ResolveItemsAsync(
        DbContext context, List<EntityEntry<IAuditable>> tracked, CancellationToken cancellationToken)
    {
        var wanted = tracked
            .Select(e => e.Entity as IAuditSubject)
            .Where(s => s?.AuditWorkItemId is not null && (s.AuditProjectId is null || s.AuditLabel is null))
            .Select(s => s!.AuditWorkItemId!.Value)
            .Distinct()
            .ToList();
        var found = new Dictionary<Guid, (Guid, string)>();
        if (wanted.Count == 0)
        {
            return found;
        }

        foreach (var item in context.ChangeTracker.Entries<WorkItem>().Where(e => wanted.Contains(e.Entity.Id)))
        {
            found[item.Entity.Id] = (item.Entity.ProjectId, item.Entity.Ref);
        }

        var missing = wanted.Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var rows = await context.Set<WorkItem>().AsNoTracking().Where(i => missing.Contains(i.Id)).Select(i => new { i.Id, i.ProjectId, i.Ref }).ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                found[row.Id] = (row.ProjectId, row.Ref);
            }
        }

        return found;
    }

    private static AuditLogEntry? Describe(
        EntityEntry entry, DateTimeOffset now, Guid? userId, string source, string correlation, Dictionary<Guid, (Guid ProjectId, string Ref)> items)
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

        var subject = entry.Entity as IAuditSubject;
        var itemId = subject?.AuditWorkItemId;
        var projectId = subject?.AuditProjectId;
        var label = subject?.AuditLabel;
        if (itemId is { } id && items.TryGetValue(id, out var item))
        {
            projectId ??= item.ProjectId;
            label ??= item.Ref;
        }

        return new AuditLogEntry
        {
            Entity = schema is null ? table! : $"{schema}.{table}",
            EntityId = entityId,
            Action = action,
            Before = before,
            After = after,
            UserId = userId,
            At = now,
            ProjectId = projectId,
            WorkItemId = itemId,
            SubjectLabel = label is { Length: > 200 } ? label[..200] : label,
            Source = source,
            CorrelationId = correlation,
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
