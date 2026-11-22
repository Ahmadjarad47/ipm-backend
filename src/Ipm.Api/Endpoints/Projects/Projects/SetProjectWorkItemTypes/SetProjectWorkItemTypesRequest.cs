namespace Ipm.Api.Endpoints.Projects.Projects.SetProjectWorkItemTypes;

public sealed record SetProjectWorkItemTypesRequest(IReadOnlyList<Guid>? AllowedTypeIds, Guid DefaultTypeId);
