using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Common;

/// <summary>One status. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record StatusResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Color,
    StatusCategory Category,
    StatusScope Scope,
    int Order,
    bool Active,
    uint Version);
