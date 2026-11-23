using System.Text.Json;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Security;
using Ipm.Application.Features.Git.Webhooks.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Webhooks.Commands.ReceiveWebhook;

internal sealed class ReceiveWebhookCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork, ISecretProtector protector, TimeProvider time)
    : ICommandHandler<ReceiveWebhookCommand, WebhookResult>
{
    public async ValueTask<Result<WebhookResult>> Handle(ReceiveWebhookCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GitProvider>(command.Provider, ignoreCase: true, out var provider))
        {
            return GitErrors.RepositoryNotFound;
        }

        var repository = await db.GitRepositories.SingleOrDefaultAsync(r => r.Id == command.RepositoryId && r.Provider == provider, cancellationToken);
        if (repository is null)
        {
            return GitErrors.RepositoryNotFound;
        }

        var connector = GitConnector.For(provider);
        var headers = command.Headers;
        var deliveryId = headers.GetValueOrDefault(connector.DeliveryHeader, string.Empty);
        var stamp = headers.GetValueOrDefault(GitConnector.TimestampHeader, string.Empty);
        var eventName = GitConnector.NormalizeEvent(headers.GetValueOrDefault(connector.EventHeader, "unknown"));
        var now = time.GetUtcNow();

        async Task<Result<WebhookResult>> RejectAsync(Error error, bool signatureValid, string message)
        {
            db.WebhookDeliveries.Add(WebhookDelivery.Create(
                repository.ProjectId, repository.Id, provider, string.IsNullOrWhiteSpace(deliveryId) ? "-" : deliveryId, eventName, signatureValid,
                DeliveryOutcome.Rejected, message, [], null, now));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return error;
        }

        if (string.IsNullOrWhiteSpace(deliveryId) || string.IsNullOrWhiteSpace(stamp))
        {
            return await RejectAsync(GitErrors.MissingHeaders, signatureValid: false, "معرّف التسليم أو الطابع الزمني مفقود");
        }

        var secret = repository.IsConnected && repository.WebhookSecretCipher is { } cipher ? protector.Unprotect(cipher) : null;
        if (secret is null || !connector.Verify(secret, command.Raw, headers.GetValueOrDefault(connector.SignatureHeader), stamp))
        {
            return await RejectAsync(GitErrors.InvalidSignature, signatureValid: false, "توقيع غير صالح");
        }

        // R5: the timestamp is signed, so it cannot be swapped; a delivery outside the window is a replay.
        var at = GitConnector.ParseTimestamp(stamp);
        if (at is null || (now - at.Value).Duration() > GitConnector.ReplayWindow)
        {
            return await RejectAsync(GitErrors.StaleDelivery, signatureValid: true, at is null ? "طابع زمني غير صالح" : "تسليم خارج نافذة 5 دقائق (حماية من إعادة الإرسال)");
        }

        // R6: the same delivery is never handled twice, whatever its first outcome (a failed one is retried only by redelivery).
        if (await db.WebhookDeliveries.AnyAsync(
                d => d.RepositoryId == repository.Id && d.DeliveryId == deliveryId && (d.Outcome == DeliveryOutcome.Processed || d.Outcome == DeliveryOutcome.Failed), cancellationToken))
        {
            db.WebhookDeliveries.Add(WebhookDelivery.Create(
                repository.ProjectId, repository.Id, provider, deliveryId, eventName, true, DeliveryOutcome.Duplicate, "تسليم مكرر؛ لم يُعالج مجدداً", [], null, now));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new WebhookResult(DeliveryOutcome.Duplicate, "تسليم مكرر؛ لم يُعالج مجدداً", []);
        }

        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == repository.ProjectId, cancellationToken);
        try
        {
            var processed = await GitIngestion.ProcessAsync(db, repository, project, eventName, command.Raw, deliveryId, cancellationToken);
            if (processed.IsSuccess)
            {
                repository.RecordSync(now);
                db.WebhookDeliveries.Add(WebhookDelivery.Create(project.Id, repository.Id, provider, deliveryId, eventName, true, DeliveryOutcome.Processed, null, processed.Value, null, now));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new WebhookResult(DeliveryOutcome.Processed, null, processed.Value);
            }

            return await FailAsync(project.Id, repository.Id, provider, deliveryId, eventName, processed.Error.Description, command.Raw, now, cancellationToken);
        }
        catch (JsonException)
        {
            return await FailAsync(project.Id, repository.Id, provider, deliveryId, eventName, "الحمولة ليست JSON صالحاً", command.Raw, now, cancellationToken);
        }
    }

    /// <summary>R7: a failed handling keeps the body for redelivery, and the events it half-added are discarded.</summary>
    private async Task<Result<WebhookResult>> FailAsync(
        Guid projectId, Guid repositoryId, GitProvider provider, string deliveryId, string eventName, string message, string raw, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var entry in db.GitEvents.Local.Where(e => e.Id != Guid.Empty && e.Version == 0).ToList())
        {
            db.GitEvents.Remove(entry);
        }

        db.WebhookDeliveries.Add(WebhookDelivery.Create(projectId, repositoryId, provider, deliveryId, eventName, true, DeliveryOutcome.Failed, message, [], raw, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WebhookResult(DeliveryOutcome.Failed, message, []);
    }
}
