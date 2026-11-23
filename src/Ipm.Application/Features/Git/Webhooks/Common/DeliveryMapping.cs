using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Webhooks.Common;

internal static class DeliveryMapping
{
    public static DeliveryResponse ToResponse(this WebhookDelivery d) =>
        new(d.Id, d.ProjectId, d.RepositoryId, d.Provider, d.DeliveryId, d.Event, d.SignatureValid, d.Outcome, d.Message, d.LinkedRefs, d.CreatedAt);
}
