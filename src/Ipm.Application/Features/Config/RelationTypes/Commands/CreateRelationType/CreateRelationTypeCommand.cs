using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.CreateRelationType;

public sealed record CreateRelationTypeCommand(
    string Key,
    string Name,
    string InverseName,
    bool Directed,
    bool Acyclic,
    bool Active,
    IReadOnlyList<TypePair> AllowedPairs) : ICommand<RelationTypeResponse>;
