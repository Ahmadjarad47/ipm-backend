using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;

namespace Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypes;

/// <summary>Every relation type. No paging: this is a short list of settings.</summary>
public sealed record GetRelationTypesQuery(bool? Active) : IQuery<IReadOnlyList<RelationTypeResponse>>;
