using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;

namespace Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypeById;

public sealed record GetRelationTypeByIdQuery(Guid Id) : IQuery<RelationTypeResponse>;
