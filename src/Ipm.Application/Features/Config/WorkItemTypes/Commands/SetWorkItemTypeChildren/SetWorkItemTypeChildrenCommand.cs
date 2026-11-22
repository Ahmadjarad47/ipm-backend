using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeChildren;

/// <summary>Replaces the types allowed as direct children of this type (R5). The type itself is allowed.</summary>
public sealed record SetWorkItemTypeChildrenCommand(Guid Id, IReadOnlyList<Guid> ChildTypeIds, uint ExpectedVersion)
    : ICommand<WorkItemTypeResponse>;
