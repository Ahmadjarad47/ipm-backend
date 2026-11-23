using System.Globalization;
using Ipm.Application.Abstractions.Events;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Behaviors;
using Ipm.Application.Features.Identity.Access;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Mediator is source generated: handlers are discovered at compile time, no reflection at runtime.
        // Pipeline order (outer -> inner): logging -> validation -> query caching -> handler.
        services.AddMediator((Mediator.MediatorOptions options) =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(AssemblyReference)];
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(QueryCachingBehavior<,>),
            ];
        });

        // Default FluentValidation messages in Arabic, whatever the server culture is.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("ar");

        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        AddDomainEventHandlers(services);

        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<Features.Activity.Notifications.Common.NotificationEmitter>();

        return services;
    }

    private static void AddDomainEventHandlers(IServiceCollection services)
    {
        var handlers = typeof(AssemblyReference).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlers)
        {
            services.AddScoped(service, implementation);
        }
    }
}
