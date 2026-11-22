using System.Security.Claims;
using Ipm.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Ipm.Infrastructure.Authentication;

/// <summary>Loads the current user access once per request.</summary>
internal static class AccessResolver
{
    private const string ItemKey = "ipm.access";

    /// <param name="context">The current request; the result is kept in its Items.</param>
    /// <param name="access">Source of the user access (Redis, then PostgreSQL).</param>
    /// <param name="principal">
    /// The principal being validated. During JWT validation <c>HttpContext.User</c> is not set yet, so the
    /// bearer handler passes the token principal; later callers use <c>HttpContext.User</c>.
    /// </param>
    public static async Task<UserAccess?> GetAsync(HttpContext? context, IAccessService access, ClaimsPrincipal? principal = null)
    {
        if (context is null)
        {
            return null;
        }

        if (context.Items.TryGetValue(ItemKey, out var cached) && cached is UserAccess known)
        {
            return known;
        }

        var sub = (principal ?? context.User).FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var id))
        {
            return null;
        }

        var result = await access.GetAsync(id, context.RequestAborted);
        context.Items[ItemKey] = result;
        return result;
    }
}
