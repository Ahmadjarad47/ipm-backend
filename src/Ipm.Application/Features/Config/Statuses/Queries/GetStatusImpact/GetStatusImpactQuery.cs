using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatusImpact;

/// <summary>Read-only preview before deactivating a status or removing it from a workflow (R15).</summary>
public sealed record GetStatusImpactQuery(Guid Id) : IQuery<ImpactResponse>;
