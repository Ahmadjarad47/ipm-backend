using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ArchiveWorkItem;

/// <summary>Archives a work item that has no active children (R3). Work items are never deleted.</summary>
public sealed record ArchiveWorkItemCommand(Guid Id, uint ExpectedVersion) : ICommand;
