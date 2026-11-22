using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.TransitionWorkItem;

/// <summary>Moves the item to another status through its workflow (R7 R8).</summary>
public sealed record TransitionWorkItemCommand(Guid Id, Guid ToStatusId, string? Comment, uint ExpectedVersion) : ICommand<WorkItemResponse>;
