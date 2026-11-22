using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Ipm.IntegrationTests.Probe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.IntegrationTests.Foundation;

/// <summary>Scenario 8 of docs/modules/00-foundation.md: ETag / If-Match over PostgreSQL xmin.</summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(ApiFactory factory)
{
    private static readonly Uri Items = new("/api/v1/_probe/items", UriKind.Relative);

    [Fact]
    public async Task Create_and_read_return_the_same_etag()
    {
        using var client = factory.CreateClient();

        var (id, etag) = await CreateAsync(client, "first");
        var read = await client.GetAsync(ItemUri(id));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(etag, read.Headers.ETag);
    }

    [Fact]
    public async Task Update_with_current_etag_succeeds_and_changes_the_etag()
    {
        using var client = factory.CreateClient();
        var (id, etag) = await CreateAsync(client, "before");

        var response = await PutAsync(client, id, "after", etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        Assert.NotEqual(etag, response.Headers.ETag);
    }

    [Fact]
    public async Task Second_update_with_the_same_etag_is_412()
    {
        using var client = factory.CreateClient();
        var (id, etag) = await CreateAsync(client, "shared");

        var first = await PutAsync(client, id, "user A", etag);
        var second = await PutAsync(client, id, "user B", etag);
        var problem = await Problem.ReadAsync(second);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal("Concurrency.StaleVersion", problem.Code);
        Assert.Equal("تم تعديل السجل من مستخدم آخر", problem.Title);
    }

    [Fact]
    public async Task Update_without_if_match_is_428()
    {
        using var client = factory.CreateClient();
        var (id, _) = await CreateAsync(client, "no header");

        var response = await PutAsync(client, id, "x", etag: null);
        var problem = await Problem.ReadAsync(response);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal("Concurrency.IfMatchRequired", problem.Code);
    }

    [Fact]
    public async Task Update_with_a_malformed_if_match_is_400()
    {
        using var client = factory.CreateClient();
        var (id, _) = await CreateAsync(client, "bad header");

        using var request = new HttpRequestMessage(HttpMethod.Put, ItemUri(id)) { Content = JsonContent.Create(new { name = "x" }) };
        request.Headers.TryAddWithoutValidation("If-Match", "not-an-etag");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Concurrency.IfMatchInvalid", (await Problem.ReadAsync(response)).Code);
    }

    [Fact]
    public async Task Delete_with_a_stale_etag_is_412_and_keeps_the_record()
    {
        using var client = factory.CreateClient();
        var (id, etag) = await CreateAsync(client, "keep me");
        await PutAsync(client, id, "changed", etag);

        using var request = new HttpRequestMessage(HttpMethod.Delete, ItemUri(id));
        request.Headers.IfMatch.Add(etag);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ItemUri(id))).StatusCode);
    }

    [Fact]
    public async Task Save_after_someone_else_saved_the_same_row_throws_concurrency_exception()
    {
        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = ProbeItem.Create("race");
        dbA.Add(item);
        await dbA.SaveChangesAsync();

        var copyB = await dbB.Set<ProbeItem>().SingleAsync(p => p.Id == item.Id);
        copyB.Rename("B wins");
        await dbB.SaveChangesAsync();

        item.Rename("A loses");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbA.SaveChangesAsync());
    }

    internal static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(Items, new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Assert.NotNull(response.Headers.ETag);
        return (body!.Id, response.Headers.ETag);
    }

    internal static async Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string name, EntityTagHeaderValue? etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, ItemUri(id)) { Content = JsonContent.Create(new { name }) };
        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        return await client.SendAsync(request);
    }

    internal static Uri ItemUri(Guid id) => new($"/api/v1/_probe/items/{id}", UriKind.Relative);
}
