using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ipm.Api.Endpoints;
using Ipm.Infrastructure;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Probe;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public const string AdminUsername = "admin";
    public const string AdminPassword = "Admin12345";
    public const string DemoPassword = "Demo12345";

    // Same images as docker-compose.yml.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("ipm_tests")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();

    /// <summary>The fake Git server every sync talks to.</summary>
    public FakeGitRemote GitRemote { get; } = new();

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

        await Services.SeedDatabaseAsync(Services.GetRequiredService<IConfiguration>());
    }

    /// <summary>A client signed in as the given user (Bearer access token). The response of the sign-in is kept for cookie tests.</summary>
    public async Task<HttpClient> SignedInClientAsync(string username, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { username, password });
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<SessionDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    public Task<HttpClient> AdminClientAsync() => SignedInClientAsync(AdminUsername, AdminPassword);

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
        builder.UseSetting("Seed:OnStartup", "false");
        builder.UseSetting("Seed:Demo", "true");
        builder.UseSetting("Seed:AdminUsername", AdminUsername);
        builder.UseSetting("Seed:AdminEmail", "admin@factory.example");
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789-abcdef");
        builder.UseSetting("RateLimiting:AuthPerMinute", "1000");
        builder.UseSetting("Git:SecretKey", "dGVzdHMtZ2l0LXNlY3JldC1rZXktMDEyMzQ1Njc4OWE=");

        builder.ConfigureServices(services =>
        {
            services.ConfigureDbContext<ApplicationDbContext>(options =>
                options.ReplaceService<IModelCustomizer, ProbeModelCustomizer>());
            services.AddTransient<IEndpoint, ProbeEndpoints>();
            services.RemoveAll<Ipm.Application.Abstractions.Git.IGitRemote>();
            services.AddSingleton(GitRemote);
            services.AddSingleton<Ipm.Application.Abstractions.Git.IGitRemote>(GitRemote);
        });
    }
}

public sealed record SessionDto(string AccessToken, DateTimeOffset ExpiresAt, bool MustChangePassword);

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
