using System.Text.Json;

namespace Ipm.Api.Endpoints.Work.WorkItems.ChangeWorkItemType;

public sealed record ChangeWorkItemTypeRequest(Guid TypeId, Dictionary<string, JsonElement>? Fields = null);
