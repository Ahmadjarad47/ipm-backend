using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.RelationTypes.Common;

/// <summary>One relation type. <see cref="Version"/> is sent as the ETag. Empty <see cref="AllowedPairs"/> means any pair of types.</summary>
public sealed record RelationTypeResponse(
    Guid Id,
    string Key,
    string Name,
    string InverseName,
    bool Directed,
    bool Acyclic,
    bool Active,
    IReadOnlyList<TypePair> AllowedPairs,
    uint Version);
