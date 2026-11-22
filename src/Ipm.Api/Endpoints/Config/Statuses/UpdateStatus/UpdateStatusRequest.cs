using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.Statuses.UpdateStatus;

/// <summary><c>key</c> and <c>scope</c> are optional and only checked (a different value is refused). Omitted <c>order</c> and <c>active</c> keep their value.</summary>
public sealed record UpdateStatusRequest(
    string Name,
    string? Description,
    string Color,
    StatusCategory Category,
    int? Order,
    bool? Active,
    string? Key,
    StatusScope? Scope);
