using Ipm.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Relations.Common;

internal static class RelationCycles
{
    /// <summary>R5: true when adding source to target of this type would close a loop, that is when the target already leads back to the source.</summary>
    public static async Task<bool> WouldCloseAsync(IApplicationDbContext db, Guid relationTypeId, Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        var edges = await db.WorkItemRelations.AsNoTracking()
            .Where(r => r.RelationTypeId == relationTypeId)
            .Select(r => new { r.SourceId, r.TargetId })
            .ToListAsync(cancellationToken);
        var next = edges.ToLookup(e => e.SourceId, e => e.TargetId);

        var stack = new Stack<Guid>([targetId]);
        var seen = new HashSet<Guid>();
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == sourceId)
            {
                return true;
            }

            if (!seen.Add(node))
            {
                continue;
            }

            foreach (var child in next[node])
            {
                stack.Push(child);
            }
        }

        return false;
    }
}
