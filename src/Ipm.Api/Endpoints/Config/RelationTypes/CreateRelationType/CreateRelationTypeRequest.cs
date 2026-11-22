using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.RelationTypes.CreateRelationType;

public sealed record CreateRelationTypeRequest(
    string Key,
    string Name,
    string InverseName,
    bool Directed,
    bool Acyclic,
    bool? Active,
    IReadOnlyList<TypePair>? AllowedPairs);
