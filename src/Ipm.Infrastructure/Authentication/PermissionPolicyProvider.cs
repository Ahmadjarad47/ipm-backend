using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Authentication;

/// <summary>Turns a permission key used as a policy name into a policy, so no policy has to be registered by hand.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        Permissions.IsKnown(policyName)
            ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(policyName)).Build()
            : await base.GetPolicyAsync(policyName);
}
