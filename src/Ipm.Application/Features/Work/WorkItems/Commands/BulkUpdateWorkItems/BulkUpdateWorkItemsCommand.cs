using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;

/// <summary>Applies one change to up to 100 items; each item is checked and saved on its own, so one refusal never stops the others (R12).</summary>
public sealed record BulkUpdateWorkItemsCommand(IReadOnlyList<Guid> Ids, BulkAction Action) : ICommand<IReadOnlyList<BulkItemResult>>;
