using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.BulkDecideApprovals;

internal sealed class BulkDecideApprovalsCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<BulkDecideApprovalsCommand, IReadOnlyList<BulkDecideResult>>
{
    public async ValueTask<Result<IReadOnlyList<BulkDecideResult>>> Handle(BulkDecideApprovalsCommand command, CancellationToken cancellationToken)
    {
        var ids = command.Ids.Distinct().ToList();
        if (ids.Count is 0 or > ApprovalPolicy.MaxBulk)
        {
            return ApprovalErrors.BulkInvalid;
        }

        var results = new List<BulkDecideResult>(ids.Count);
        foreach (var id in ids)
        {
            // The caller must state the revision they reviewed; guessing the current one would approve what they never saw.
            if (!command.Revisions.TryGetValue(id, out var revision) || revision <= 0)
            {
                results.Add(new BulkDecideResult(id, false, "يجب تحديد رقم الإصدار الذي راجعته لكل طلب."));
                continue;
            }

            var guarded = await ApprovalGuard.LoadAsync(db, currentUser, access, time, id, track: true, cancellationToken);
            if (guarded.IsFailure)
            {
                results.Add(new BulkDecideResult(id, false, guarded.Error.Description));
                continue;
            }

            var decided = await ApprovalDecisions.DecideAsync(guarded.Value, DecisionKind.Approve, command.Comment, revision, securityLog, time, cancellationToken);
            if (decided.IsFailure)
            {
                results.Add(new BulkDecideResult(id, false, decided.Error.Description));
                continue;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            results.Add(new BulkDecideResult(id, true, null));
        }

        return results;
    }
}
