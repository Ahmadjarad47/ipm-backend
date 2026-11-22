using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeHistory;

/// <summary>The change history of one type (FR-TYP-10), newest first, from <c>audit_log</c>.</summary>
public sealed record GetWorkItemTypeHistoryQuery(Guid Id, PageRequest Page) : IQuery<PagedList<AuditRecord>>;
