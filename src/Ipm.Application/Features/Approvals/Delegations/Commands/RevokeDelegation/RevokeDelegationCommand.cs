using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Approvals.Delegations.Commands.RevokeDelegation;

/// <summary>Cancels a delegation (R13): the one who delegated, or a holder of approvals.configure.</summary>
public sealed record RevokeDelegationCommand(Guid Id) : ICommand;
