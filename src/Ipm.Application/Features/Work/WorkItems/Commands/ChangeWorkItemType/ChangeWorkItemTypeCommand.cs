using System.Text.Json;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ChangeWorkItemType;

/// <summary>Changes the type of an item when nothing conflicts (R10). With conflicts nothing changes and they are returned.</summary>
/// <remarks><paramref name="Fields"/> are extra values (usually the new type required fields) merged over the ones the item holds.</remarks>
public sealed record ChangeWorkItemTypeCommand(Guid Id, Guid TypeId, uint ExpectedVersion, IReadOnlyDictionary<string, JsonElement>? Fields = null) : ICommand<TypeChangeResponse>;
