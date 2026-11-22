using Ipm.Api.Endpoints;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Probe;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Ipm.IntegrationTests.Infrastructure;

/// <summary>
/// Starts PostgreSQL and Redis containers once per test run and hosts the real API against them,
/// The foundation part adds a test-only "probe" table and
/// endpoints (see <see cref="ProbeItem"/>) to exercise audit, ETag and error handling.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{

    // Same images as docker-compose.yml.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("ipm_tests")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    public string DatabaseConnectionString => _postgres.GetConnectionString();

    /// <summary>A second, empty database on the same server, for migration tests.</summary>
    public string EmptyDatabaseConnectionString =>
        new Npgsql.NpgsqlConnectionStringBuilder(DatabaseConnectionString) { Database = "ipm_migrations" }.ConnectionString;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        await _postgres.ExecScriptAsync("CREATE DATABASE ipm_migrations;");

        // The probe table is not part of the migrations, so the test database is created from the model.
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        }

    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }

    /// <summary>Empties the probe and audit tables so a test starts from a known state.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE test.probe_items, public.audit_log;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", DatabaseConnectionString);
        builder.UseSetting("ConnectionStrings:Cache", _redis.GetConnectionString());
        builder.UseSetting("OpenApi:Enabled", "true");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Serilog:WriteTo:1:Args:serverUrl", "http://localhost:1");

        builder.ConfigureServices(services =>
        {
            services.ConfigureDbContext<ApplicationDbContext>(options =>
                options.ReplaceService<IModelCustomizer, ProbeModelCustomizer>());
            services.AddTransient<IEndpoint, ProbeEndpoints>();
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
