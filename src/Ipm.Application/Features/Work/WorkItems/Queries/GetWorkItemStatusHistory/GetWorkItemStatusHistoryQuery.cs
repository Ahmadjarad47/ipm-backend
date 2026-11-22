using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemStatusHistory;

/// <summary>The status changes of a work item the caller can see, newest first.</summary>
public sealed record GetWorkItemStatusHistoryQuery(Guid Id, PageRequest Page) : IQuery<PagedList<WorkItemStatusChangeResponse>>;
