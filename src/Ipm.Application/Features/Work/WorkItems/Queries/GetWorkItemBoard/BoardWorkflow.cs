namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemBoard;

/// <summary>The workflow items of <paramref name="TypeId"/> follow in the project (the project override, else the type own).</summary>
public sealed record BoardWorkflow(Guid TypeId, Guid WorkflowId, IReadOnlyList<Guid> StatusIds);
