using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Config.Workflows.Queries.GetWorkflowHistory;

/// <summary>The change history of one workflow, newest first, from <c>audit_log</c>.</summary>
public sealed record GetWorkflowHistoryQuery(Guid Id, PageRequest Page) : IQuery<PagedList<AuditRecord>>;
