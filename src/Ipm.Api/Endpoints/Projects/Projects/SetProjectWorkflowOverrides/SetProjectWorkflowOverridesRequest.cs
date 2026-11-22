namespace Ipm.Api.Endpoints.Projects.Projects.SetProjectWorkflowOverrides;

/// <summary>Work item type id to the workflow id this project uses for it instead of the type own.</summary>
public sealed record SetProjectWorkflowOverridesRequest(Dictionary<Guid, Guid>? Overrides);
