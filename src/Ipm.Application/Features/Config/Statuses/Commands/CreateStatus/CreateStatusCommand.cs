using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Commands.CreateStatus;

public sealed record CreateStatusCommand(
    string Key,
    string Name,
    string Description,
    string Color,
    StatusCategory Category,
    StatusScope Scope,
    int Order,
    bool Active) : ICommand<StatusResponse>;
