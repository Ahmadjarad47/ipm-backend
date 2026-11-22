namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetHierarchyRules;

public sealed record HierarchyRulesResponse(IReadOnlyList<HierarchyPair> Pairs);
