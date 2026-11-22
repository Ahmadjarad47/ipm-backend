using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetHierarchyRules;

/// <summary>The hierarchy of all types as flat (parent, child) pairs, for the matrix screen.</summary>
public sealed record GetHierarchyRulesQuery : IQuery<HierarchyRulesResponse>;
