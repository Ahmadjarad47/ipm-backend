using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>A work item whose project the caller may see (and act on, when a permission was asked for), with the project and the caller access.</summary>
internal sealed record GuardedWorkItem(WorkItem Item, Project Project, UserAccess Caller);
