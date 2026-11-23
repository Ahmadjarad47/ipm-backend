using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Infrastructure.Persistence.Auditing;

/// <summary>Spec 08: the audit log and the security events as one readable, filtered, newest-first feed.</summary>
internal sealed class AuditFeed(ApplicationDbContext db, TimeProvider time, Microsoft.AspNetCore.Http.IHttpContextAccessor http) : IAuditFeed
{
    private const int ChunkSize = 1000;

    public async Task<PagedList<AuditEventResponse>> SearchAsync(AuditFilter filter, AuditVisibility visibility, PageRequest page, CancellationToken cancellationToken)
    {
        var audit = AuditRows(filter, visibility);
        var security = SecurityRows(filter, visibility);
        var auditTotal = audit is null ? 0 : await audit.CountAsync(cancellationToken);
        var securityTotal = security is null ? 0 : await security.CountAsync(cancellationToken);

        var want = page.Skip + page.Take;
        var auditRows = audit is null ? [] : await audit.OrderByDescending(e => e.Seq).Take(want).ToListAsync(cancellationToken);
        var securityRows = security is null ? [] : await security.OrderByDescending(e => e.At).ThenByDescending(e => e.Id).Take(want).ToListAsync(cancellationToken);

        var names = await NamesAsync([.. auditRows.Select(r => r.UserId), .. securityRows.Select(r => r.UserId)], cancellationToken);
        var merged = auditRows.Select(r => (r.At, r.Id, Event: ToEvent(r, names)))
            .Concat(securityRows.Select(r => (r.At, r.Id, Event: ToEvent(r, names))))
            .OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
            .Skip(page.Skip).Take(page.Take)
            .Select(x => x.Event)
            .ToList();

        return new PagedList<AuditEventResponse>(merged, Math.Max(page.Page, 1), page.Take, auditTotal + securityTotal);
    }

    public async Task<int> CountAsync(AuditFilter filter, AuditVisibility visibility, CancellationToken cancellationToken)
    {
        var audit = AuditRows(filter, visibility);
        var security = SecurityRows(filter, visibility);
        return (audit is null ? 0 : await audit.CountAsync(cancellationToken)) + (security is null ? 0 : await security.CountAsync(cancellationToken));
    }

    public async IAsyncEnumerable<AuditEventResponse> StreamAsync(
        AuditFilter filter, AuditVisibility visibility, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var page = 1; ; page++)
        {
            var chunk = await SearchAsync(filter, visibility, new PageRequest(page, ChunkSize), cancellationToken);
            foreach (var item in chunk.Items)
            {
                yield return item;
            }

            if (!chunk.HasNextPage)
            {
                yield break;
            }
        }
    }

    public async Task<AuditVerification> VerifyAsync(CancellationToken cancellationToken)
    {
        var total = await db.Set<AuditLogEntry>().LongCountAsync(cancellationToken);
        var broken = await db.Database.SqlQuery<Guid>($"""
            WITH c AS (
                SELECT id, seq, prev_hash, hash,
                    lag(hash) OVER (ORDER BY seq) AS prev_row_hash,
                    lag(seq) OVER (ORDER BY seq) AS prev_seq,
                    public.audit_log_payload(seq, entity, entity_id, action, before, after, user_id, at, project_id, work_item_id, subject_label, source, correlation_id) AS payload
                FROM public.audit_log)
            SELECT id AS "Value" FROM c
            WHERE prev_hash <> coalesce(prev_row_hash, '') OR seq <> coalesce(prev_seq, 0) + 1
                OR hash <> encode(sha256(convert_to(prev_hash || payload, 'UTF8')), 'hex')
            ORDER BY seq LIMIT 1
            """).ToListAsync(cancellationToken);
        return new AuditVerification(broken.Count == 0, total, broken.Count == 0 ? null : broken[0]);
    }

    public async Task RecordExportAsync(Guid? userId, string filters, int count, CancellationToken cancellationToken)
    {
        var (source, correlation) = AuditLogInterceptor.OriginOf(http.HttpContext);
        db.Set<AuditLogEntry>().Add(new AuditLogEntry
        {
            Entity = "audit.exports",
            EntityId = Guid.CreateVersion7(),
            Action = AuditAction.Insert,
            After = System.Text.Json.JsonSerializer.Serialize(new { filters = System.Text.Json.JsonDocument.Parse(filters).RootElement, count }),
            UserId = userId,
            At = time.GetUtcNow(),
            Source = source,
            CorrelationId = correlation,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Null when the filter cannot match any audit row (it asks for security events only, or for another source).</summary>
    private IQueryable<AuditLogEntry>? AuditRows(AuditFilter filter, AuditVisibility visibility)
    {
        if (filter.Security == true)
        {
            return null;
        }

        IQueryable<AuditLogEntry> rows = db.Set<AuditLogEntry>().AsNoTracking();
        if (!visibility.All)
        {
            var projects = visibility.ProjectIds.ToList();
            rows = rows.Where(e => e.ProjectId != null && projects.Contains(e.ProjectId.Value));
        }

        if (filter.From is { } from)
        {
            rows = rows.Where(e => e.At >= from);
        }

        if (filter.To is { } to)
        {
            rows = rows.Where(e => e.At < to);
        }

        if (filter.ActorId is { } actor)
        {
            rows = rows.Where(e => e.UserId == actor);
        }

        if (filter.SubjectId is { } subject)
        {
            rows = rows.Where(e => e.EntityId == subject);
        }

        if (filter.ProjectId is { } project)
        {
            rows = rows.Where(e => e.ProjectId == project);
        }

        if (filter.WorkItemId is { } item)
        {
            rows = rows.Where(e => e.WorkItemId == item);
        }

        if (filter.Source is { } source)
        {
            rows = rows.Where(e => e.Source == source);
        }

        if (filter.CorrelationId is { } correlation)
        {
            rows = rows.Where(e => e.CorrelationId == correlation);
        }

        if (filter.SubjectType is { } type)
        {
            var tables = AuditVocabulary.Rules.Where(r => r.SubjectType == type).Select(r => r.Table).Distinct().ToList();
            rows = rows.Where(e => tables.Contains(e.Entity));
        }

        if (filter.Action is { } action)
        {
            var predicate = ActionPredicate(action);
            if (predicate is null)
            {
                return rows.Where(e => false);
            }

            rows = rows.Where(predicate);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            rows = rows.Where(e => (e.SubjectLabel != null && EF.Functions.ILike(e.SubjectLabel, term)) || EF.Functions.ILike(e.Entity, term));
        }

        return rows;
    }

    /// <summary>Null when the caller may not see security events or the filter excludes them.</summary>
    private IQueryable<SecurityEvent>? SecurityRows(AuditFilter filter, AuditVisibility visibility)
    {
        if (!visibility.All || filter.Security == false || filter.ProjectId is not null || filter.WorkItemId is not null || filter.CorrelationId is not null
            || (filter.Source is not null && filter.Source != "api") || (filter.SubjectType is not null && filter.SubjectType != "user"))
        {
            return null;
        }

        IQueryable<SecurityEvent> rows = db.Set<SecurityEvent>().AsNoTracking();
        if (filter.From is { } from)
        {
            rows = rows.Where(e => e.At >= from);
        }

        if (filter.To is { } to)
        {
            rows = rows.Where(e => e.At < to);
        }

        if (filter.ActorId is { } actor)
        {
            rows = rows.Where(e => e.UserId == actor);
        }

        if (filter.SubjectId is { } subject)
        {
            rows = rows.Where(e => e.UserId == subject);
        }

        if (filter.Action is { } action)
        {
            var types = AuditVocabulary.Security.Where(s => s.Value.Key == action).Select(s => s.Key).ToList();
            rows = rows.Where(e => types.Contains(e.Type));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            rows = rows.Where(e => e.Username != null && EF.Functions.ILike(e.Username, term));
        }

        return rows;
    }

    /// <summary>The rows an action key stands for: the rule's table and kind of change, with its column changed and no earlier column rule of that table matching.</summary>
    private static Expression<Func<AuditLogEntry, bool>>? ActionPredicate(string key)
    {
        Expression<Func<AuditLogEntry, bool>>? any = null;
        foreach (var rule in AuditVocabulary.Rules.Where(r => r.Key == key))
        {
            var table = rule.Table;
            var kind = rule.Action;
            Expression<Func<AuditLogEntry, bool>> condition = e => e.Entity == table && e.Action == kind;
            if (rule.Column is { } column)
            {
                condition = And(condition, e => EF.Functions.JsonExists(e.After!, column));
            }

            foreach (var earlier in AuditVocabulary.Rules.TakeWhile(r => r != rule).Where(r => r.Table == table && r.Action == kind && r.Column is not null && r.Column != rule.Column))
            {
                var earlierColumn = earlier.Column!;
                condition = And(condition, e => !EF.Functions.JsonExists(e.After!, earlierColumn));
            }

            any = any is null ? condition : Or(any, condition);
        }

        return any;
    }

    private static Expression<Func<AuditLogEntry, bool>> And(Expression<Func<AuditLogEntry, bool>> a, Expression<Func<AuditLogEntry, bool>> b) =>
        Expression.Lambda<Func<AuditLogEntry, bool>>(Expression.AndAlso(a.Body, Rebind(b, a.Parameters[0])), a.Parameters);

    private static Expression<Func<AuditLogEntry, bool>> Or(Expression<Func<AuditLogEntry, bool>> a, Expression<Func<AuditLogEntry, bool>> b) =>
        Expression.Lambda<Func<AuditLogEntry, bool>>(Expression.OrElse(a.Body, Rebind(b, a.Parameters[0])), a.Parameters);

    private static Expression Rebind(Expression<Func<AuditLogEntry, bool>> lambda, ParameterExpression parameter) =>
        new ParameterReplacer(lambda.Parameters[0], parameter).Visit(lambda.Body);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.OfType<Guid>().Distinct().ToList();
        return wanted.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
    }

    private static AuditEventResponse ToEvent(AuditLogEntry row, Dictionary<Guid, string> names)
    {
        var (key, type, label) = AuditVocabulary.Describe(row.Entity, row.Action, row.After);
        var actor = row.UserId is { } id && names.TryGetValue(id, out var name) ? name : null;
        return new AuditEventResponse(
            row.Id, row.Seq, row.At, row.UserId, actor, key, type, row.EntityId, row.SubjectLabel, row.ProjectId,
            AuditVocabulary.Changes(row.Action, row.Before, row.After), row.Source, row.CorrelationId, Security: false,
            Summary(actor, label, row.SubjectLabel));
    }

    private static AuditEventResponse ToEvent(SecurityEvent row, Dictionary<Guid, string> names)
    {
        var (key, label) = AuditVocabulary.Security[row.Type];
        var actor = row.UserId is { } id && names.TryGetValue(id, out var name) ? name : row.Username;
        return new AuditEventResponse(
            row.Id, 0, row.At, row.UserId, actor, key, "user", row.UserId ?? Guid.Empty, row.Username, null, [], "api", string.Empty, Security: true,
            Summary(actor, label, row.Permission));
    }

    private static string Summary(string? actor, string label, string? subject) =>
        string.Join(' ', new[] { actor ?? "النظام", label, subject }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
