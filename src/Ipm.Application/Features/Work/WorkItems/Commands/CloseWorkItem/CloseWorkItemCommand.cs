using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.CloseWorkItem;

/// <summary>
/// Moves the item to a done status its workflow allows from where it is (R9). With one such status it is chosen automatically;
/// with several, <paramref name="ToStatusId"/> is required.
/// </summary>
public sealed record CloseWorkItemCommand(Guid Id, Guid? ToStatusId, string? Comment, uint ExpectedVersion) : ICommand<WorkItemResponse>;
