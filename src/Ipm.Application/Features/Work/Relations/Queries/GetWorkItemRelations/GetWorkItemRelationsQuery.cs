using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Relations.Common;

namespace Ipm.Application.Features.Work.Relations.Queries.GetWorkItemRelations;

/// <summary>The links of a work item, from both sides, hiding those whose other item the caller cannot see (R8).</summary>
public sealed record GetWorkItemRelationsQuery(Guid Id) : IQuery<IReadOnlyList<RelationViewResponse>>;
