using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>The result of a type change: the item when it changed, or the conflicts that stopped it.</summary>
public sealed record TypeChangeResponse(WorkItemResponse? Item, IReadOnlyList<TypeChangeConflict> Conflicts);
