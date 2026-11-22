using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Common;

/// <summary>Checks that need the database, shared by the relation type commands.</summary>
internal static class RelationTypeChecks
{
    /// <summary>R8: every type named in an allowed pair exists.</summary>
    public static async Task<Result> EnsurePairTypesExistAsync(
        IApplicationDbContext db, IReadOnlyCollection<TypePair> pairs, CancellationToken cancellationToken)
    {
        var typeIds = pairs.SelectMany(p => new[] { p.SourceTypeId, p.TargetTypeId }).Distinct().ToList();
        if (typeIds.Count == 0)
        {
            return Result.Success();
        }

        var existing = await db.WorkItemTypes.CountAsync(t => typeIds.Contains(t.Id), cancellationToken);
        return existing == typeIds.Count ? Result.Success() : RelationTypeErrors.PairTypeNotFound;
    }
}
