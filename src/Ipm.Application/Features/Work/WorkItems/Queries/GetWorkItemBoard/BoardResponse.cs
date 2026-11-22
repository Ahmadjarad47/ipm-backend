namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemBoard;

/// <summary>The board of a project: the workflow of each allowed type, and its items.</summary>
public sealed record BoardResponse(Guid ProjectId, IReadOnlyList<BoardWorkflow> Workflows, IReadOnlyList<BoardItem> Items);
