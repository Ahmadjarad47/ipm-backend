using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Activity.Feeds.Queries.GetWorkItemActivity;

/// <summary>Spec 08 R9: what happened to a work item: its own changes, its comments, links and approval requests, newest first.</summary>
public sealed record GetWorkItemActivityQuery(Guid WorkItemId, PageRequest Page) : IQuery<PagedList<AuditEventResponse>>;
