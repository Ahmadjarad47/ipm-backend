using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Abstractions.Auditing;

/// <summary>
/// Reads the change history of one record from <c>audit_log</c>, newest first (the writer is an EF Core interceptor).
/// <c>entity</c> is the schema-qualified table, e.g. <c>config.work_item_types</c>.
/// </summary>
public interface IAuditLogReader
{
    Task<PagedList<AuditRecord>> ReadAsync(string entity, Guid entityId, PageRequest page, CancellationToken cancellationToken = default);
}
