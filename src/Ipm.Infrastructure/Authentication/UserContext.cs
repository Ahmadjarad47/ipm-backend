using System.Security.Claims;
using Ipm.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Ipm.Infrastructure.Authentication;

/// <summary>
/// Reads the current user from the JWT claims of the HTTP request.
/// Token issuing arrives in part 01 (identity).
/// </summary>
public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public bool InRequest => httpContextAccessor.HttpContext is not null;

    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
