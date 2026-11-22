using System.Net;
using System.Text.Json;
using Ipm.Infrastructure.Persistence;
using Ipm.Infrastructure.Persistence.Auditing;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.IntegrationTests.Foundation;

/// <summary>Scenario 7 of docs/modules/00-foundation.md: every change leaves a row in audit_log.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuditLogTests(ApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insert_update_and_delete_are_each_recorded_with_before_and_after()
    {
        using var client = factory.CreateClient();
        var (id, etag) = await ConcurrencyTests.CreateAsync(client, "old name");
        var update = await ConcurrencyTests.PutAsync(client, id, "new name", etag);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, ConcurrencyTests.ItemUri(id));
        delete.Headers.IfMatch.Add(update.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        var rows = await AuditRowsAsync(id);
        Assert.Equal([AuditAction.Insert, AuditAction.Update, AuditAction.Delete], rows.Select(r => r.Action));
        Assert.All(rows, r => Assert.Equal("test.probe_items", r.Entity));

        var insert = rows[0];
        Assert.Null(insert.Before);
        Assert.Equal("old name", Field(insert.After, "name"));

        var updated = rows[1];
        Assert.Equal("old name", Field(updated.Before, "name"));
        Assert.Equal("new name", Field(updated.After, "name"));
        Assert.False(Has(updated.After, "createdAt"), "an update stores only the columns that changed");

        var deleted = rows[2];
        Assert.Equal("new name", Field(deleted.Before, "name"));
        Assert.Null(deleted.After);
    }

    [Fact]
    public async Task Excluded_columns_and_the_version_never_reach_the_audit_log()
    {
        using var client = factory.CreateClient();
        var (id, _) = await ConcurrencyTests.CreateAsync(client, "secret holder");

        var insert = Assert.Single(await AuditRowsAsync(id));

        Assert.False(Has(insert.After, "secret"));
        Assert.False(Has(insert.After, "version"));
        Assert.DoesNotContain("hidden-", insert.After, StringComparison.Ordinal);
    }

    private async Task<List<AuditLogEntry>> AuditRowsAsync(Guid entityId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<AuditLogEntry>().Where(e => e.EntityId == entityId).OrderBy(e => e.Id).ToListAsync();
    }

    private static bool Has(string? json, string field) =>
        json is not null && JsonDocument.Parse(json).RootElement.TryGetProperty(field, out _);

    private static string? Field(string? json, string field) =>
        JsonDocument.Parse(json!).RootElement.GetProperty(field).GetString();
}
