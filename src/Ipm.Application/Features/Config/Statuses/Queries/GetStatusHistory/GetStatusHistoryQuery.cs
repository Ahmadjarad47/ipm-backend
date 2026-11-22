using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatusHistory;

/// <summary>The change history of one status, newest first, from <c>audit_log</c>.</summary>
public sealed record GetStatusHistoryQuery(Guid Id, PageRequest Page) : IQuery<PagedList<AuditRecord>>;
