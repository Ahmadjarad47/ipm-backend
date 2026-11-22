using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ChangeWorkItemType;

/// <summary>Changes the type of an item when nothing conflicts (R10). With conflicts nothing changes and they are returned.</summary>
public sealed record ChangeWorkItemTypeCommand(Guid Id, Guid TypeId, uint ExpectedVersion) : ICommand<TypeChangeResponse>;
