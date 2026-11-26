using System.Text.Json;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Queries.CheckWorkItemTypeChange;

/// <summary>Lists what would stop the item from becoming <paramref name="TypeId"/>, without changing anything (R10).</summary>
public sealed record CheckWorkItemTypeChangeQuery(Guid Id, Guid TypeId, IReadOnlyDictionary<string, JsonElement>? Fields = null) : IQuery<TypeChangeCheckResponse>;
