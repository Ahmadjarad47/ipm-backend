namespace Ipm.Application.Features.Dashboard.Stats.Common;

/// <summary>Spec 09 R10, in the shape the front end reads. Category keys are the status categories in kebab case (not-started, active, blocked, done, cancelled).</summary>
public sealed record DashboardStatsResponse(
    int ActiveProjects,
    int TotalProjects,
    int OpenWorkItems,
    int OverdueWorkItems,
    int PendingApprovals,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyDictionary<string, int> LeafByCategory,
    IReadOnlyList<DashboardTypeStats> ByType,
    int BlockedItems,
    DashboardDependencies Dependencies,
    int GitActivity);
