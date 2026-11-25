namespace Ipm.RealTime;

/// <summary>Hub URLs. Mounted under /api/v1 so they ride the existing nginx/dev-proxy rule for that prefix.</summary>
public static class RealTimeRoutes
{
    public const string HubsPrefix = "/api/v1/hubs";

    public const string Notifications = HubsPrefix + "/notifications";
}
