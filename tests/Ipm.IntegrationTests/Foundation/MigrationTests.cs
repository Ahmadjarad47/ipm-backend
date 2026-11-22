using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ipm.IntegrationTests.Foundation;

/// <summary>Phase 2 exit check, reused by every part: migrations apply to an empty database and match the model.</summary>
[Collection(ApiCollection.Name)]
public sealed class MigrationTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrations_apply_to_an_empty_database_and_nothing_is_missing_from_them()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(factory.EmptyDatabaseConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public"))
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new ApplicationDbContext(options);

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges(), "the model changed without a migration: run dotnet ef migrations add");
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM public.audit_log").SingleAsync());
    }
}
