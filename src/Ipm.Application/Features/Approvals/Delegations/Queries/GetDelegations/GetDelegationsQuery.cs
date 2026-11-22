using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Delegations.Common;

namespace Ipm.Application.Features.Approvals.Delegations.Queries.GetDelegations;

/// <summary>The delegations the caller gave or received, newest first; a holder of approvals.configure sees all (R13).</summary>
public sealed record GetDelegationsQuery : IQuery<IReadOnlyList<DelegationResponse>>;
