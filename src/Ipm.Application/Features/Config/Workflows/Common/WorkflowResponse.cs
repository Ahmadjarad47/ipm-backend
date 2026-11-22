using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Common;

/// <summary>One workflow with its statuses and transitions. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record WorkflowResponse(
    Guid Id,
    string Key,
    string Name,
    StatusScope Scope,
    IReadOnlyList<Guid> StatusIds,
    Guid InitialStatusId,
    IReadOnlyList<WorkflowTransition> Transitions,
    uint Version);
