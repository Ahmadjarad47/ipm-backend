using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Activity.Audit.Queries.VerifyAudit;

internal sealed class VerifyAuditQueryHandler(IUserContext currentUser, IAccessService access, IAuditFeed feed) : IQueryHandler<VerifyAuditQuery, AuditVerification>
{
    public async ValueTask<Result<AuditVerification>> Handle(VerifyAuditQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        return caller.Value.Has(Permissions.AuditView)
            ? await feed.VerifyAsync(cancellationToken)
            : AuthErrors.Forbidden;
    }
}
