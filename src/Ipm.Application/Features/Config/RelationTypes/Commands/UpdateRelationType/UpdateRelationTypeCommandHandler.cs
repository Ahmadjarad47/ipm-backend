using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.UpdateRelationType;

internal sealed class UpdateRelationTypeCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRelationTypeCommand, RelationTypeResponse>
{
    public async ValueTask<Result<RelationTypeResponse>> Handle(UpdateRelationTypeCommand command, CancellationToken cancellationToken)
    {
        var type = await db.RelationTypes.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (type is null)
        {
            return RelationTypeErrors.NotFound;
        }

        if (!type.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var keyUnchanged = type.EnsureKeyUnchanged(command.Key); // R6
        if (keyUnchanged.IsFailure)
        {
            return keyUnchanged.Error;
        }

        var pairs = await RelationTypeChecks.EnsurePairTypesExistAsync(db, command.AllowedPairs, cancellationToken);
        if (pairs.IsFailure)
        {
            return pairs.Error; // R8
        }

        // R9 (part 02, enforced from part 05b): once links exist, direction and the cycle rule are frozen and pairs cannot be narrowed under them.
        // R10 (a switched-off type refuses new links) is checked where links are created.
        var used = await db.WorkItemRelations.AsNoTracking().Where(r => r.RelationTypeId == type.Id).ToListAsync(cancellationToken);
        if (used.Count > 0)
        {
            if (command.Directed != type.IsDirected || command.Acyclic != type.IsAcyclic)
            {
                return RelationTypeErrors.LockedAfterUse("لا يمكن تغيير اتجاه نوع الرابط أو منع الحلقات بعد استعماله بروابط فعلية.");
            }

            var ids = used.SelectMany(r => new[] { r.SourceId, r.TargetId }).Distinct().ToList();
            var items = await db.WorkItems.AsNoTracking().Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.Ref, i.TypeId }).ToDictionaryAsync(i => i.Id, cancellationToken);
            var broken = used
                .Where(r => items.TryGetValue(r.SourceId, out var s) && items.TryGetValue(r.TargetId, out var t) && !type.AllowsPair(s.TypeId, t.TypeId, command.AllowedPairs))
                .Select(r => $"{items[r.SourceId].Ref} → {items[r.TargetId].Ref}")
                .ToList();
            if (broken.Count > 0)
            {
                return RelationTypeErrors.LockedAfterUse($"لا يمكن تضييق الأزواج المسموحة: توجد روابط قائمة ستصبح غير صالحة ({string.Join("، ", broken)}).");
            }
        }

        var updated = type.Update(command.Name, command.InverseName, command.Directed, command.Acyclic, command.Active ?? type.IsActive, command.AllowedPairs); // R7
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return type.ToResponse();
    }
}
