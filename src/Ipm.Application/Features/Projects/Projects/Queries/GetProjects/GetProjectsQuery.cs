using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjects;

/// <summary>
/// The projects the caller can see. <paramref name="Search"/> matches the code or the name. A date range keeps the projects whose start
/// (or end) date falls inside it. <paramref name="Sort"/> is <c>code</c>, <c>name</c>, <c>startDate</c> or <c>endDate</c>.
/// </summary>
public sealed record GetProjectsQuery(
    string? Search,
    Guid? StatusId,
    Guid? OwnerId,
    Guid? MemberId,
    bool? Archived,
    DateOnly? StartFrom,
    DateOnly? StartTo,
    DateOnly? EndFrom,
    DateOnly? EndTo,
    string? Sort,
    bool Descending,
    PageRequest Page) : IQuery<PagedList<ProjectResponse>>;
