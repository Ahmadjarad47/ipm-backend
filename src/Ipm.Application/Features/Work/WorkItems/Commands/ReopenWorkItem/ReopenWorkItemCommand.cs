using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ReopenWorkItem;

/// <summary>
/// Moves a done or cancelled item back to an open status (R9). The reason is required. When <paramref name="ToStatusId"/> is omitted
/// the first open status the workflow allows is used.
/// </summary>
public sealed record ReopenWorkItemCommand(Guid Id, Guid? ToStatusId, string Comment, uint ExpectedVersion) : ICommand<WorkItemResponse>;
