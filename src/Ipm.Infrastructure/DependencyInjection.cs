using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Caching;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Git;
using Ipm.Application.Abstractions.Security;
using Ipm.Infrastructure.Git;
using Ipm.Infrastructure.Security;
using Ipm.Infrastructure.Authentication;
using Ipm.Infrastructure.Caching;
using Ipm.Infrastructure.Events;
using Ipm.Infrastructure.Persistence;
using Ipm.Infrastructure.Persistence.Auditing;
using Ipm.Infrastructure.Persistence.Interceptors;
using Ipm.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var database = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");
        var cache = configuration.GetConnectionString("Cache")
            ?? throw new InvalidOperationException("Connection string 'Cache' is not configured.");

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        AddPersistence(services, database);
        services.AddIdentityAuthentication(configuration);

        services.AddOptions<GitOptions>().Bind(configuration.GetSection(GitOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddHttpClient<IGitRemote, GitRemote>((sp, client) =>
            client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitOptions>>().Value.RemoteTimeoutSeconds));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = cache;
            options.InstanceName = "ipm:";
        });
        services.AddSingleton<ICacheService, RedisCacheService>();

        services.AddHealthChecks()
            .AddNpgSql(database, name: "postgresql")
            .AddRedis(cache, name: "redis");

        return services;
    }

    private static void AddPersistence(IServiceCollection services, string connectionString)
    {
        services.AddScoped<DomainEventsDispatcher>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<DomainEventsInterceptor>();

        // Interceptor order matters: stamp created/updated first, then audit (so "after" includes the stamps).
        services.AddDbContext<ApplicationDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<AuditLogInterceptor>(),
                sp.GetRequiredService<DomainEventsInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<Ipm.Application.Abstractions.Settings.IOrganizationSettings, OrganizationSettings>();
        services.AddHostedService<SettingsRefresher>();
        services.AddScoped<Ipm.Application.Abstractions.Auditing.IAuditFeed, AuditFeed>();
        services.AddScoped<IWorkItemNumbering, WorkItemNumbering>();
    }

    /// <summary>What every installation needs (built-in roles, the administrator) and, with <c>Seed:Demo</c>, demo data. One seeder per part, in dependency order.</summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await IdentitySeeder.SeedAsync(services, configuration);
        await SettingsSeeder.SeedAsync(services);
        await ConfigSeeder.SeedAsync(services, configuration);
        await ProjectsSeeder.SeedAsync(services, configuration);
        await WorkSeeder.SeedAsync(services, configuration);
        await ApprovalsSeeder.SeedAsync(services, configuration);
        await GitSeeder.SeedAsync(services, configuration);
        await ActivitySeeder.SeedAsync(services, configuration);
    }

    /// <summary>Applies pending EF Core migrations. Used at startup in development and docker-compose.</summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
