using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.RelationTypes.UpdateRelationType;

/// <summary><c>key</c> is optional and only checked (a different key is refused). Omitted <c>active</c> keeps its value.</summary>
public sealed record UpdateRelationTypeRequest(
    string Name,
    string InverseName,
    bool Directed,
    bool Acyclic,
    bool? Active,
    IReadOnlyList<TypePair>? AllowedPairs,
    string? Key);
