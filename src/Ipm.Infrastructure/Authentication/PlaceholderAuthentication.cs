using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Authentication;

/// <summary>
/// Part 00 has no sign-in yet, but every endpoint under /api/v1 already requires one. This scheme authenticates
/// nobody, so protected endpoints answer 401 (Arabic problem+json) instead of failing. Part 01 replaces it with JWT.
/// </summary>
public static class PlaceholderAuthentication
{
    public const string Scheme = "Placeholder";

    public static IServiceCollection AddPlaceholderAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, NobodyHandler>(Scheme, null);
        services.AddAuthorization();
        return services;
    }

    private sealed class NobodyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    }
}
