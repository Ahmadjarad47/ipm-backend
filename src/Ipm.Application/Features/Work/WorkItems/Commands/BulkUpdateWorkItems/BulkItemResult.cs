namespace Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;

/// <summary>The outcome for one selected item (R12).</summary>
public sealed record BulkItemResult(Guid Id, string? Ref, bool Ok, string? Message);
