using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Delegations.Common;

namespace Ipm.Application.Features.Approvals.Delegations.Commands.CreateDelegation;

/// <summary>Hands approval authority to another user for a period (R13). <paramref name="FromUserId"/> defaults to the caller; another user needs approvals.configure.</summary>
public sealed record CreateDelegationCommand(Guid? FromUserId, Guid ToUserId, DateOnly From, DateOnly To, string Reason) : ICommand<DelegationResponse>;
