using System.Text.Json;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Webhooks.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Webhooks.Commands.RedeliverWebhook;

internal sealed class RedeliverWebhookCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<RedeliverWebhookCommand, DeliveryResponse>
{
    public async ValueTask<Result<DeliveryResponse>> Handle(RedeliverWebhookCommand command, CancellationToken cancellationToken)
    {
        var delivery = await db.WebhookDeliveries.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken);
        if (delivery is null)
        {
            return GitErrors.DeliveryNotFound;
        }

        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, delivery.ProjectId, Permissions.GitManage, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == Ipm.Domain.Projects.ProjectErrors.NotFound ? GitErrors.DeliveryNotFound : guarded.Error;
        }

        if (delivery.Outcome != DeliveryOutcome.Failed || delivery.Payload is null)
        {
            return GitErrors.NotRedeliverable;
        }

        var repository = await db.GitRepositories.SingleAsync(r => r.Id == delivery.RepositoryId, cancellationToken);
        try
        {
            var processed = await GitIngestion.ProcessAsync(db, repository, guarded.Value.Project, delivery.Event, delivery.Payload, delivery.DeliveryId, cancellationToken);
            if (processed.IsSuccess)
            {
                delivery.MarkProcessed(processed.Value, "أُعيدت المعالجة يدوياً");
            }
            else
            {
                delivery.RecordRetryFailure(processed.Error.Description);
            }
        }
        catch (JsonException)
        {
            delivery.RecordRetryFailure("الحمولة ليست JSON صالحاً");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return delivery.ToResponse();
    }
}
