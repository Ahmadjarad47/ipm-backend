using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ipm.Infrastructure.Authentication;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // User is the Identity user; no Identity roles (our Role carries the permissions).
        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<ISecurityEventLog, SecurityEventLog>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false; // keep "sub", "email" as they are
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectStaleSessionsAsync };
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    /// <summary>
    /// A valid signature is not enough: the user must still exist and be active (R3), and the token security
    /// stamp must match (a password change or reset ends older sessions).
    /// </summary>
    private static async Task RejectStaleSessionsAsync(TokenValidatedContext context)
    {
        var accessService = context.HttpContext.RequestServices.GetRequiredService<IAccessService>();
        var access = await AccessResolver.GetAsync(context.HttpContext, accessService, context.Principal);
        var stamp = context.Principal?.FindFirst(TokenService.SecurityStampClaim)?.Value;

        if (access is null || !access.IsActive || access.SecurityStamp != stamp)
        {
            context.Fail("Session is no longer valid.");
        }
    }
}
