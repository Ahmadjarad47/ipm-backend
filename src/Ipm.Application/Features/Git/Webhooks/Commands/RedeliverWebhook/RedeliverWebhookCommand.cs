using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Webhooks.Common;

namespace Ipm.Application.Features.Git.Webhooks.Commands.RedeliverWebhook;

/// <summary>Processes the stored body of a failed delivery again (R9, git.manage).</summary>
public sealed record RedeliverWebhookCommand(Guid Id) : ICommand<DeliveryResponse>;
