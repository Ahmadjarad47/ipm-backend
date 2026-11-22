using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Ipm.Infrastructure.Authentication;

/// <summary>
/// Checks the permission against the user current access (Redis, refreshed on every change), so revoking a
/// permission takes effect on the next request. A refusal is written to the security events (R21).
/// </summary>
internal sealed class PermissionAuthorizationHandler(IAccessService access, ISecurityEventLog securityLog, IHttpContextAccessor http)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var current = await AccessResolver.GetAsync(http.HttpContext, access);
        if (current is not null && current.Has(requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        if (current is not null)
        {
            await securityLog.LogAsync(
                SecurityEventType.PermissionDenied, current.UserId, null, permission: requirement.Permission);
        }
    }
}
