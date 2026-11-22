using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.Statuses.CreateStatus;

public sealed record CreateStatusRequest(
    string Key,
    string Name,
    string? Description,
    string Color,
    StatusCategory Category,
    StatusScope Scope,
    int? Order,
    bool? Active);
