using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemBoard;

/// <summary>Every non-archived item of a project in a light shape, with the workflow each type follows there (R11).</summary>
public sealed record GetWorkItemBoardQuery(Guid ProjectId) : IQuery<BoardResponse>;
