using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Commands.UpdateStatus;

/// <summary>
/// <paramref name="Key"/> and <paramref name="Scope"/> are optional and only checked: they can never change (R1 R2).
/// Omitted <paramref name="Order"/> and <paramref name="Active"/> keep their current value.
/// </summary>
public sealed record UpdateStatusCommand(
    Guid Id,
    string? Key,
    StatusScope? Scope,
    string Name,
    string Description,
    string Color,
    StatusCategory Category,
    int? Order,
    bool? Active,
    uint ExpectedVersion) : ICommand<StatusResponse>;
