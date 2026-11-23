using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.GitEvents.Common;

namespace Ipm.Application.Features.Git.GitEvents.Queries.GetGitEvents;

/// <summary>The Git events of one project, newest first (R12). <paramref name="Kind"/> is push, pull-request or merge; <paramref name="WorkItemRef"/> keeps those linked to an item.</summary>
public sealed record GetGitEventsQuery(Guid? ProjectId, string? WorkItemRef, string? Kind, string? Search, PageRequest Page) : IQuery<PagedList<GitEventResponse>>;
