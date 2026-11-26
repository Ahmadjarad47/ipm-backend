using System.Text.Json;

namespace Ipm.Api.Endpoints.Work.WorkItems.CheckWorkItemTypeChange;

public sealed record CheckWorkItemTypeChangeRequest(Guid TypeId, Dictionary<string, JsonElement>? Fields = null);
