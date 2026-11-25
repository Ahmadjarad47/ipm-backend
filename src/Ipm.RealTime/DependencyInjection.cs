using Ipm.Application.Abstractions.Realtime;
using Ipm.RealTime.Authentication;
using Ipm.RealTime.Hubs;
using Ipm.RealTime.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.RealTime;

public static class DependencyInjection
{
    public static IServiceCollection AddRealTime(this IServiceCollection services)
    {
        // Single API instance today (see docker-compose.yml), so the default in-memory SignalR server is enough.
        // Scaling to several replicas would need a backplane: services.AddSignalR().AddStackExchangeRedis(cache);
        // (Redis is already provisioned for the permission cache, so that is a one-line change later.)
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
        services.AddScoped<INotificationPusher, SignalRNotificationPusher>();

        // WebSocket connections cannot send an Authorization header, so SignalR's documented pattern is a
        // query-string token — accepted only on the hub path, so REST endpoints still require the header.
        // PostConfigure runs after the base JWT setup and keeps whatever OnMessageReceived it already set.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, bearer =>
        {
            var existing = bearer.Events.OnMessageReceived;
            bearer.Events.OnMessageReceived = async context =>
            {
                await existing(context);

                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(RealTimeRoutes.HubsPrefix))
                {
                    context.Token = accessToken;
                }
            };
        });

        return services;
    }

    public static IEndpointRouteBuilder MapRealTime(this IEndpointRouteBuilder app)
    {
        app.MapHub<NotificationsHub>(RealTimeRoutes.Notifications).RequireAuthorization();
        return app;
    }
}
