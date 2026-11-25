using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ipm.RealTime.Authentication;

/// <summary>
/// Maps a hub connection to the user id SignalR's <c>Clients.User(...)</c> uses for routing.
/// The access token carries the user id as a literal <c>"sub"</c> claim (<see cref="JwtRegisteredClaimNames.Sub"/>) because
/// token validation runs with <c>MapInboundClaims = false</c> — the default provider reads <c>ClaimTypes.NameIdentifier</c>,
/// which is never populated here, so without this override pushes would silently never reach anyone.
/// </summary>
internal sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
