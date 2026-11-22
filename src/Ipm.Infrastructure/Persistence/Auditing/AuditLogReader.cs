using System.Text.Json;
using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Infrastructure.Persistence.Auditing;

internal sealed class AuditLogReader(ApplicationDbContext db) : IAuditLogReader
{
    public async Task<PagedList<AuditRecord>> ReadAsync(string entity, Guid entityId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var rows = db.Set<AuditLogEntry>().AsNoTracking().Where(e => e.Entity == entity && e.EntityId == entityId);
        var total = await rows.CountAsync(cancellationToken);

        var items = await (
                from e in rows
                join u in db.Users on e.UserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby e.Id descending
                select new { e.At, e.UserId, UserName = u == null ? null : u.Name, e.Action, e.Before, e.After })
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        var records = items
            .Select(i => new AuditRecord(i.At, i.UserId, i.UserName, JsonNamingPolicy.KebabCaseLower.ConvertName(i.Action.ToString()), Parse(i.Before), Parse(i.After)))
            .ToList();

        return new PagedList<AuditRecord>(records, Math.Max(page.Page, 1), page.Take, total);
    }

    private static JsonElement? Parse(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
