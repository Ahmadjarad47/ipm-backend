using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjectStatusHistory;

/// <summary>The status changes of a project the caller can see, newest first.</summary>
public sealed record GetProjectStatusHistoryQuery(Guid Id, PageRequest Page) : IQuery<PagedList<ProjectStatusChangeResponse>>;
