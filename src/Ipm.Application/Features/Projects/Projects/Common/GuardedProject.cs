using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>A project the caller may see and (when a permission was asked for) may act on, with the caller access.</summary>
internal sealed record GuardedProject(Project Project, UserAccess Caller);
