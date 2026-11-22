using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Identity.Auth.Queries.GetMe;

/// <summary>The signed-in user and everything the front end needs to build its menus (same shape as its AuthSession).</summary>
public sealed record GetMeQuery : IQuery<MeResponse>;
