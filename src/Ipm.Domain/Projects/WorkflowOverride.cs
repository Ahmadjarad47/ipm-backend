namespace Ipm.Domain.Projects;

/// <summary>For one work item type, the workflow this project uses instead of the type own workflow (FR-WF-10).</summary>
public sealed record WorkflowOverride(Guid TypeId, Guid WorkflowId);
