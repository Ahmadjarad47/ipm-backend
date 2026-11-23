namespace Ipm.Application.Features.Dashboard.Stats.Common;

/// <summary>Leaf items of one work item type, cancelled ones left out of the total.</summary>
public sealed record DashboardTypeStats(Guid TypeId, int Total, int Done, IReadOnlyDictionary<string, int> ByCategory);
