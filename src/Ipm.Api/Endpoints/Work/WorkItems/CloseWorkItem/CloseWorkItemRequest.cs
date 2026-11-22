namespace Ipm.Api.Endpoints.Work.WorkItems.CloseWorkItem;

public sealed record CloseWorkItemRequest(Guid? ToStatusId, string? Comment);
