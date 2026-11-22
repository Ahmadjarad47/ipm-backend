using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ipm.Api.Endpoints;

public static class EndpointExtensions
{
    /// <summary>Registers every <see cref="IEndpoint"/> found in the assembly.</summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpoints = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t));

        services.TryAddEnumerable(endpoints);
        return services;
    }

    /// <summary>Maps every registered endpoint under the given route group (e.g. /api/v1).</summary>
    public static IApplicationBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder group)
    {
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(group);
        }

        return app;
    }
}
