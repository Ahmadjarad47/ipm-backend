namespace Ipm.Api.Endpoints.Work.WorkItems.TransitionWorkItem;

public sealed record TransitionWorkItemRequest(Guid ToStatusId, string? Comment);
