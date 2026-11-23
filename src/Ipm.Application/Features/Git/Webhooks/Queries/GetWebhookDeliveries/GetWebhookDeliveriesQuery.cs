using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.Webhooks.Common;

namespace Ipm.Application.Features.Git.Webhooks.Queries.GetWebhookDeliveries;

/// <summary>The webhook calls of one project (git.view), newest first (R9). The project is required.</summary>
public sealed record GetWebhookDeliveriesQuery(Guid? ProjectId, PageRequest Page) : IQuery<PagedList<DeliveryResponse>>;
