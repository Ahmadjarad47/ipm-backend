using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeImpact;

/// <summary>Read-only preview before deactivating a type or changing its workflow (R15).</summary>
public sealed record GetWorkItemTypeImpactQuery(Guid Id) : IQuery<ImpactResponse>;
