namespace Ipm.Application.Features.Config.WorkItemTypes.Common;

/// <summary>A project or workflow named in an impact answer.</summary>
public sealed record ImpactRef(Guid Id, string Name);
