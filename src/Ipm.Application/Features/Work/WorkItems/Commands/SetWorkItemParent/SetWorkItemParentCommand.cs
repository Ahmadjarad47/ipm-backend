using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.SetWorkItemParent;

/// <summary>Moves an item under another parent, or out of its parent when <paramref name="ParentId"/> is null (R6).</summary>
public sealed record SetWorkItemParentCommand(Guid Id, Guid? ParentId, uint ExpectedVersion) : ICommand<WorkItemResponse>;
