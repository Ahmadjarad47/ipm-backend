using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Webhooks.Common;

namespace Ipm.Application.Features.Git.Webhooks.Commands.ReceiveWebhook;

/// <summary>A call from a Git provider (R5 to R8). It has no user: the signature authenticates it. <paramref name="Headers"/> is case-insensitive.</summary>
public sealed record ReceiveWebhookCommand(string Provider, Guid RepositoryId, string Raw, IReadOnlyDictionary<string, string> Headers) : ICommand<WebhookResult>;
