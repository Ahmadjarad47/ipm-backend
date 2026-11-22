using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.SetWorkItemTypeFields;

public sealed record SetWorkItemTypeFieldsRequest(IReadOnlyList<FieldDefinition>? Fields);
