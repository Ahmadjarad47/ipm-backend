using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.UpdateRelationType;

/// <summary><paramref name="Key"/> is optional and only checked: it can never change (R6).</summary>
public sealed record UpdateRelationTypeCommand(
    Guid Id,
    string? Key,
    string Name,
    string InverseName,
    bool Directed,
    bool Acyclic,
    bool? Active,
    IReadOnlyList<TypePair> AllowedPairs,
    uint ExpectedVersion) : ICommand<RelationTypeResponse>;
