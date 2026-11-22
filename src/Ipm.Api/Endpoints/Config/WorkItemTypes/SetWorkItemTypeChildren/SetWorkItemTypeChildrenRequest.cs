namespace Ipm.Api.Endpoints.Config.WorkItemTypes.SetWorkItemTypeChildren;

public sealed record SetWorkItemTypeChildrenRequest(IReadOnlyList<Guid>? ChildTypeIds);
