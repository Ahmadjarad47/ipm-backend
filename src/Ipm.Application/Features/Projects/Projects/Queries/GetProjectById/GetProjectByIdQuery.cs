using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjectById;

/// <summary>A project the caller can see. Any other project answers as if it did not exist (404).</summary>
public sealed record GetProjectByIdQuery(Guid Id) : IQuery<ProjectResponse>;
