using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.Webhooks.Common;

namespace Ipm.Application.Features.Git.Webhooks.Queries.GetWebhookDeliveries;

/// <summary>The webhook calls of one project (git.view), newest first (R9). The project is required; <paramref name="Outcome"/> (processed, duplicate, rejected, failed) and <paramref name="Event"/> narrow the list.</summary>
public sealed record GetWebhookDeliveriesQuery(Guid? ProjectId, string? Outcome, string? Event, PageRequest Page) : IQuery<PagedList<DeliveryResponse>>;
