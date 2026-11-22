namespace Ipm.Api.Endpoints.Work.WorkItems.ReopenWorkItem;

public sealed record ReopenWorkItemRequest(Guid? ToStatusId, string Comment);
