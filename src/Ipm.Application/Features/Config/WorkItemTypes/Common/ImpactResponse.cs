namespace Ipm.Application.Features.Config.WorkItemTypes.Common;

/// <summary>What a change to a type or a status would touch (R15): items, projects, workflows, and where it is the default.</summary>
public sealed record ImpactResponse(int Items, int Projects, int Workflows, IReadOnlyList<ImpactRef> AsDefaultIn);
