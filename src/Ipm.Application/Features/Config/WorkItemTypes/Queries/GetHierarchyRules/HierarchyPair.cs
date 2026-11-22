namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetHierarchyRules;

/// <summary>One allowed parent → child type combination (FR-REL-01).</summary>
public sealed record HierarchyPair(Guid ParentTypeId, Guid ChildTypeId);
