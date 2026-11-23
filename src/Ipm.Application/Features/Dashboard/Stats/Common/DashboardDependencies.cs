namespace Ipm.Application.Features.Dashboard.Stats.Common;

/// <summary>The open dependency picture: items waiting on an open blocker, distinct blockers, and the share of open items that wait.</summary>
public sealed record DashboardDependencies(int BlockedItems, int BlockingItems, int ItemsWithOpenDependencyPct);
