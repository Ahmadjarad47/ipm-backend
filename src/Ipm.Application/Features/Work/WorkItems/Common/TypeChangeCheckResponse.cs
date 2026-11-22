using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>What stops a type change (R10). Empty means the change is possible.</summary>
public sealed record TypeChangeCheckResponse(IReadOnlyList<TypeChangeConflict> Conflicts);
