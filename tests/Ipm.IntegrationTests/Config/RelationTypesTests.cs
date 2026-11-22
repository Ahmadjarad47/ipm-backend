using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Config.ConfigApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Config;

/// <summary>Scenarios 1, 2 and 9–11 of docs/modules/02-work-item-types.md for relation types.</summary>
[Collection(ApiCollection.Name)]
public sealed class RelationTypesTests(ApiFactory factory)
{
    private static readonly Uri Relations = new("/api/v1/relation-types", UriKind.Relative);

    [Fact]
    public async Task Scenario_1_the_three_default_relation_types_are_readable_by_any_signed_in_user()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);

        var blocks = await RelationTypeByKeyAsync(noura, "blocks");
        var relates = await RelationTypeByKeyAsync(noura, "relates");
        var duplicates = await RelationTypeByKeyAsync(noura, "duplicates");

        Assert.True(blocks.GetProperty("directed").GetBoolean());
        Assert.True(blocks.GetProperty("acyclic").GetBoolean());
        Assert.Equal("محجوب بواسطة", blocks.GetProperty("inverseName").GetString());
        Assert.False(relates.GetProperty("directed").GetBoolean());
        Assert.Equal(relates.GetProperty("name").GetString(), relates.GetProperty("inverseName").GetString());
        Assert.True(duplicates.GetProperty("acyclic").GetBoolean());
        Assert.Empty(blocks.GetProperty("allowedPairs").EnumerateArray());

        var one = await noura.GetAsync(new Uri($"/api/v1/relation-types/{blocks.GetProperty("id").GetGuid()}", UriKind.Relative));
        Assert.NotNull(one.Headers.ETag);
    }

    [Fact]
    public async Task Scenario_2_only_the_relation_type_manager_creates_types()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var body = new { key = NewKey("rel"), name = "يتبع", inverseName = "يتبعه", directed = true, acyclic = false };

        Assert.Equal(HttpStatusCode.Forbidden, (await noura.PostAsJsonAsync(Relations, body)).StatusCode);
        var created = await admin.PostAsJsonAsync(Relations, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        Assert.True((await ReadAsync(created)).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Scenario_9_R6_keys_are_unique_ignoring_case_well_formed_and_never_change()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("rk");
        var (id, etag) = await CreateRelationTypeAsync(admin, key);

        var duplicate = await admin.PostAsJsonAsync(Relations, new { key = key.ToUpperInvariant(), name = "x", inverseName = "y", directed = true, acyclic = false });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("RelationType.KeyTaken", (await ProblemAsync(duplicate)).Code);

        var malformed = await admin.PostAsJsonAsync(Relations, new { key = "9 bad", name = "x", inverseName = "y", directed = true, acyclic = false });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("RelationType.KeyInvalid", (await ProblemAsync(malformed)).Code);

        var changed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/relation-types/{id}",
            new { key = "other_key", name = "x", inverseName = "y", directed = true, acyclic = false }, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("RelationType.KeyImmutable", (await ProblemAsync(changed)).Code);
    }

    [Fact]
    public async Task Scenario_9_R7_an_undirected_type_reads_the_same_from_both_sides_and_cannot_forbid_cycles()
    {
        using var admin = await factory.AdminClientAsync();

        var mismatch = await admin.PostAsJsonAsync(Relations, new { key = NewKey("u"), name = "مرتبط", inverseName = "مرتبط عكسياً", directed = false, acyclic = false });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal("RelationType.InverseNameMustMatch", (await ProblemAsync(mismatch)).Code);

        var acyclic = await admin.PostAsJsonAsync(Relations, new { key = NewKey("u"), name = "مرتبط", inverseName = "مرتبط", directed = false, acyclic = true });
        Assert.Equal(HttpStatusCode.BadRequest, acyclic.StatusCode);
        Assert.Equal("RelationType.AcyclicNeedsDirected", (await ProblemAsync(acyclic)).Code);

        var fine = await admin.PostAsJsonAsync(Relations, new { key = NewKey("u"), name = "مرتبط", inverseName = "مرتبط", directed = false, acyclic = false });
        Assert.Equal(HttpStatusCode.Created, fine.StatusCode);
    }

    [Fact]
    public async Task Scenario_9_R8_allowed_pairs_must_name_existing_types()
    {
        using var admin = await factory.AdminClientAsync();
        var feature = (await TypeByKeyAsync(admin, "feature")).GetProperty("id").GetGuid();
        var bug = (await TypeByKeyAsync(admin, "bug")).GetProperty("id").GetGuid();

        var unknown = await admin.PostAsJsonAsync(
            Relations,
            new { key = NewKey("p"), name = "x", inverseName = "y", directed = true, acyclic = false, allowedPairs = new[] { new { sourceTypeId = feature, targetTypeId = Guid.NewGuid() } } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("RelationType.PairTypeNotFound", (await ProblemAsync(unknown)).Code);

        var known = await admin.PostAsJsonAsync(
            Relations,
            new { key = NewKey("p"), name = "x", inverseName = "y", directed = true, acyclic = false, allowedPairs = new[] { new { sourceTypeId = feature, targetTypeId = bug } } });
        Assert.Equal(HttpStatusCode.Created, known.StatusCode);
        var pairs = (await ReadAsync(known)).GetProperty("allowedPairs");
        Assert.Equal(1, pairs.GetArrayLength());
        Assert.Equal(feature, pairs[0].GetProperty("sourceTypeId").GetGuid());
        Assert.Equal(bug, pairs[0].GetProperty("targetTypeId").GetGuid());
    }

    [Fact]
    public async Task Scenario_9_a_relation_type_is_updated_and_can_be_deactivated_while_staying_readable()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("up");
        var (id, etag) = await CreateRelationTypeAsync(admin, key);

        var updated = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/relation-types/{id}",
            new { name = "اسم جديد", inverseName = "عكسي جديد", directed = true, acyclic = true, active = false }, etag);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await ReadAsync(updated);
        Assert.Equal("اسم جديد", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("acyclic").GetBoolean());
        Assert.False(body.GetProperty("active").GetBoolean());
        var activeOnly = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/relation-types?active=true", UriKind.Relative)));
        Assert.DoesNotContain(key, activeOnly.EnumerateArray().Select(t => t.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task Scenario_10_a_stale_etag_is_412_and_a_missing_if_match_is_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateRelationTypeAsync(admin, NewKey("re"));
        var body = new { name = "الأول", inverseName = "عكسي", directed = true, acyclic = false };

        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{id}", body, etag);
        var second = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{id}", body, etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{id}", body, null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
    }

    [Fact]
    public async Task Scenario_11_the_history_lists_each_change_newest_first_for_relation_type_managers_only()
    {
        using var admin = await factory.AdminClientAsync();
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (id, etag) = await CreateRelationTypeAsync(admin, NewKey("rh"));
        await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{id}", new { name = "تغيّر", inverseName = "عكسي", directed = true, acyclic = false }, etag);

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/relation-types/{id}/history", UriKind.Relative)));

        Assert.Equal(["update", "insert"], history.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await noura.GetAsync(new Uri($"/api/v1/relation-types/{id}/history", UriKind.Relative))).StatusCode);
    }
}
