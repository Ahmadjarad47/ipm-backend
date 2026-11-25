using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Telegram.Common;

namespace Ipm.Application.Features.Activity.Telegram.Queries.GetMyTelegramLink;

/// <summary>Spec 11 R5: is Telegram on, and is the caller linked.</summary>
public sealed record GetMyTelegramLinkQuery : IQuery<TelegramLinkResponse>;
