using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.Webhooks.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Webhooks.Queries.GetWebhookDeliveries;

internal sealed class GetWebhookDeliveriesQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetWebhookDeliveriesQuery, PagedList<DeliveryResponse>>
{
    public async ValueTask<Result<PagedList<DeliveryResponse>>> Handle(GetWebhookDeliveriesQuery query, CancellationToken cancellationToken)
    {
        if (query.ProjectId is not { } projectId)
        {
            return GitErrors.ProjectRequired;
        }

        var guarded = await ProjectGuard.ReadWithAsync(db, currentUser, access, securityLog, projectId, Permissions.GitView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var deliveries = db.WebhookDeliveries.AsNoTracking().Where(d => d.ProjectId == projectId);
        if (query.Outcome is { Length: > 0 } outcome && Enum.TryParse<DeliveryOutcome>(outcome, ignoreCase: true, out var wanted))
        {
            deliveries = deliveries.Where(d => d.Outcome == wanted);
        }

        if (query.Event is { Length: > 0 } eventName)
        {
            deliveries = deliveries.Where(d => d.Event == eventName);
        }

        var total = await deliveries.CountAsync(cancellationToken);
        var rows = await deliveries.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).Skip(query.Page.Skip).Take(query.Page.Take).ToListAsync(cancellationToken);
        return new PagedList<DeliveryResponse>([.. rows.Select(d => d.ToResponse())], Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
