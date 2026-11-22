using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypeHistory;

/// <summary>The change history of one relation type, newest first, from <c>audit_log</c>.</summary>
public sealed record GetRelationTypeHistoryQuery(Guid Id, PageRequest Page) : IQuery<PagedList<AuditRecord>>;
