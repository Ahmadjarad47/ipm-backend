using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Relations.Common;

namespace Ipm.Application.Features.Work.Relations.Queries.GetProjectRelations;

/// <summary>The links whose two items are both in the project (R8).</summary>
public sealed record GetProjectRelationsQuery(Guid ProjectId) : IQuery<IReadOnlyList<RelationResponse>>;
