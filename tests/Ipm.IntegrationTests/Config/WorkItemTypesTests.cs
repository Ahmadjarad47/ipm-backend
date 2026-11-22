using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Config.ConfigApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Config;

/// <summary>Scenarios 1–8, 10 and 11 of docs/modules/02-work-item-types.md for work item types.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemTypesTests(ApiFactory factory)
{
    private static readonly Uri Types = new("/api/v1/work-item-types", UriKind.Relative);

    [Fact]
    public async Task Scenario_1_any_signed_in_user_reads_the_five_default_types_with_fields_and_children()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);

        var list = await ReadAsync(await noura.GetAsync(Types));
        var keys = list.EnumerateArray().Select(t => t.GetProperty("key").GetString()).ToList();
        foreach (var key in new[] { "feature", "task", "bug", "subtask", "milestone" })
        {
            Assert.Contains(key, keys);
        }

        // Display order: feature (1) comes before task (2), bug (3), subtask (4), milestone (5).
        Assert.True(keys.IndexOf("feature") < keys.IndexOf("task"));
        Assert.True(keys.IndexOf("bug") < keys.IndexOf("milestone"));

        var bug = await TypeByKeyAsync(noura, "bug");
        Assert.Equal(["severity", "environment", "reproducible"], bug.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("key").GetString()));
        Assert.Equal("select", bug.GetProperty("fields")[0].GetProperty("type").GetString());
        Assert.Equal(3, bug.GetProperty("fields")[0].GetProperty("options").GetArrayLength());
        Assert.Equal("bug", bug.GetProperty("icon").GetString());

        var feature = await TypeByKeyAsync(noura, "feature");
        var task = await TypeByKeyAsync(noura, "task");
        var bugId = bug.GetProperty("id").GetGuid();
        Assert.Equal(
            [task.GetProperty("id").GetGuid(), bugId],
            feature.GetProperty("allowedChildTypeIds").EnumerateArray().Select(i => i.GetGuid()));
    }

    [Fact]
    public async Task Scenario_1_the_hierarchy_is_shown_as_flat_pairs_and_one_type_by_id_has_an_etag()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var feature = await TypeByKeyAsync(noura, "feature");
        var task = await TypeByKeyAsync(noura, "task");

        var rules = await ReadAsync(await noura.GetAsync(new Uri("/api/v1/hierarchy-rules", UriKind.Relative)));
        var pairs = rules.GetProperty("pairs").EnumerateArray().Select(p => (p.GetProperty("parentTypeId").GetGuid(), p.GetProperty("childTypeId").GetGuid())).ToList();
        Assert.Contains((feature.GetProperty("id").GetGuid(), task.GetProperty("id").GetGuid()), pairs);

        var one = await noura.GetAsync(new Uri($"/api/v1/work-item-types/{feature.GetProperty("id").GetGuid()}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.NotNull(one.Headers.ETag);
        Assert.Equal(HttpStatusCode.NotFound, (await noura.GetAsync(new Uri($"/api/v1/work-item-types/{Guid.NewGuid()}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_1_without_a_token_nothing_is_readable()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Types)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/hierarchy-rules", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/relation-types", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_2_an_ordinary_user_cannot_create_types_but_the_administrator_can()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var body = new { key = NewKey("t"), name = "نوع", color = "#112233", icon = "epic" };

        var denied = await noura.PostAsJsonAsync(Types, body);
        var created = await admin.PostAsJsonAsync(Types, body);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        var type = await ReadAsync(created);
        Assert.Empty(type.GetProperty("fields").EnumerateArray());
        Assert.Empty(type.GetProperty("allowedChildTypeIds").EnumerateArray());
        Assert.True(type.GetProperty("active").GetBoolean());
        Assert.Equal("epic", type.GetProperty("icon").GetString());
    }

    [Fact]
    public async Task Scenario_3_fields_and_type_data_are_separate_permissions()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("sep"));
        var (fieldsRole, _) = await CreateRoleAsync(admin, Unique("fields"), RoleScope.System, [Permissions.WorkItemFieldsManage]);
        var (typesRole, _) = await CreateRoleAsync(admin, Unique("types"), RoleScope.System, [Permissions.WorkItemTypesManage]);
        var fieldsOnly = await ReadyUserAsync(factory, admin, "fld", fieldsRole);
        var typesOnly = await ReadyUserAsync(factory, admin, "typ", typesRole);

        var setFields = await SendAsync(
            fieldsOnly.Client, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new { fields = new[] { Field("note") } }, etag);
        var editByFields = await SendAsync(fieldsOnly.Client, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody(), etag);
        Assert.Equal(HttpStatusCode.OK, setFields.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, editByFields.StatusCode);

        var editByTypes = await SendAsync(typesOnly.Client, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody("اسم جديد"), setFields.Headers.ETag);
        var fieldsByTypes = await SendAsync(
            typesOnly.Client, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new { fields = new[] { Field("other") } }, editByTypes.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, editByTypes.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, fieldsByTypes.StatusCode);
    }

    [Fact]
    public async Task Scenario_4_R1_keys_are_unique_ignoring_case_well_formed_and_never_change()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("keyed");
        var (typeId, etag) = await CreateTypeAsync(admin, key);

        var duplicate = await admin.PostAsJsonAsync(Types, new { key = key.ToUpperInvariant(), name = "x", color = "#112233", icon = "task" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("WorkItemType.KeyTaken", (await ProblemAsync(duplicate)).Code);

        var malformed = await admin.PostAsJsonAsync(Types, new { key = "1-bad key", name = "x", color = "#112233", icon = "task" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("WorkItemType.KeyInvalid", (await ProblemAsync(malformed)).Code);

        var changed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody(key: "other_key"), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("WorkItemType.KeyImmutable", (await ProblemAsync(changed)).Code);

        var repeated = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody(key: key.ToUpperInvariant()), etag);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    [Fact]
    public async Task Scenario_5_R2_color_icon_and_order_are_validated_with_arabic_messages()
    {
        using var admin = await factory.AdminClientAsync();

        var badColor = await admin.PostAsJsonAsync(Types, new { key = NewKey("c"), name = "x", color = "red", icon = "task" });
        var problem = await ProblemAsync(badColor);
        Assert.Equal(HttpStatusCode.BadRequest, badColor.StatusCode);
        Assert.Contains(problem.Root.GetProperty("errors").GetProperty("color")[0].GetString()!, c => c is >= '؀' and <= 'ۿ');

        var badIcon = await admin.PostAsJsonAsync(Types, new { key = NewKey("i"), name = "x", color = "#112233", icon = "rocket" });
        Assert.Equal(HttpStatusCode.BadRequest, badIcon.StatusCode);

        var badOrder = await admin.PostAsJsonAsync(Types, new { key = NewKey("o"), name = "x", color = "#112233", icon = "task", order = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, badOrder.StatusCode);
        Assert.True((await ProblemAsync(badOrder)).Root.GetProperty("errors").TryGetProperty("order", out _));
    }

    [Fact]
    public async Task Scenario_6_R3_each_field_problem_is_reported_with_its_exact_path()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("fields"));

        var response = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields",
            new
            {
                fields = new object[]
                {
                    Field("same"),
                    Field("same"),
                    Field("pick", "select"),
                    Field("dup_options", "select", new[] { new { value = "a", label = "أ" }, new { value = "a", label = "ب" } }),
                    Field("range", "number", min: 10, max: 1),
                },
            },
            etag);
        var errors = (await ProblemAsync(response)).Root.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("fields[1].key", out _));
        Assert.True(errors.TryGetProperty("fields[2].options", out _));
        Assert.True(errors.TryGetProperty("fields[3].options", out _));
        Assert.True(errors.TryGetProperty("fields[4].max", out _));
    }

    [Fact]
    public async Task Scenario_6_R3_more_than_forty_fields_are_refused_and_a_valid_list_is_saved_and_read_back()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("many"));
        var tooMany = Enumerable.Range(0, 41).Select(i => Field($"f{i}")).ToArray();

        var refused = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new { fields = tooMany }, etag);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.True((await ProblemAsync(refused)).Root.GetProperty("errors").TryGetProperty("fields", out _));

        var valid = new[]
        {
            Field("second", "number", min: 0, max: 5, order: 2),
            Field("first", "select", new[] { new { value = "a", label = "أ" } }, order: 1),
        };
        var saved = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new { fields = valid }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.NotEqual(etag, saved.Headers.ETag);

        var (body, _) = await GetAsync(admin, $"/api/v1/work-item-types/{typeId}");
        Assert.Equal(["first", "second"], body.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("key").GetString()));
        Assert.Equal("select", body.GetProperty("fields")[0].GetProperty("type").GetString());
        Assert.Equal(5, body.GetProperty("fields")[1].GetProperty("max").GetDecimal());
    }

    [Fact]
    public async Task Scenario_7_R4_types_cannot_be_deleted_only_deactivated_and_stay_readable()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("gone");
        var (typeId, etag) = await CreateTypeAsync(admin, key);

        var delete = await admin.DeleteAsync(new Uri($"/api/v1/work-item-types/{typeId}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);

        var off = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "نوع", color = "#112233", icon = "task", active = false }, etag);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await ReadAsync(off)).GetProperty("active").GetBoolean());

        Assert.Contains(key, (await ReadAsync(await admin.GetAsync(Types))).EnumerateArray().Select(t => t.GetProperty("key").GetString()));
        var activeOnly = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/work-item-types?active=true", UriKind.Relative)));
        Assert.DoesNotContain(key, activeOnly.EnumerateArray().Select(t => t.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task Scenario_8_R5_children_must_exist_and_a_type_may_contain_itself()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("tree"));
        var subtask = (await TypeByKeyAsync(admin, "subtask")).GetProperty("id").GetGuid();

        var unknown = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/children", new { childTypeIds = new[] { Guid.NewGuid() } }, etag);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("WorkItemType.ChildTypeNotFound", (await ProblemAsync(unknown)).Code);

        var saved = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/children", new { childTypeIds = new[] { typeId, subtask, subtask } }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal([typeId, subtask], (await ReadAsync(saved)).GetProperty("allowedChildTypeIds").EnumerateArray().Select(i => i.GetGuid()));

        var rules = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/hierarchy-rules", UriKind.Relative)));
        Assert.Contains(rules.GetProperty("pairs").EnumerateArray(), p => p.GetProperty("parentTypeId").GetGuid() == typeId && p.GetProperty("childTypeId").GetGuid() == typeId);
    }

    [Fact]
    public async Task Scenario_10_a_stale_etag_is_412_and_a_missing_if_match_is_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("etag"));

        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody("الأول"), etag);
        var second = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody("الثاني"), etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody("الثالث"), null);
        var staleFields = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new { fields = Array.Empty<object>() }, etag);
        var staleChildren = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/children", new { childTypeIds = Array.Empty<Guid>() }, etag);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal("Concurrency.StaleVersion", (await ProblemAsync(second)).Code);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleFields.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleChildren.StatusCode);
    }

    [Fact]
    public async Task Scenario_11_every_change_is_in_the_history_newest_first_and_only_for_type_managers()
    {
        using var admin = await factory.AdminClientAsync();
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("hist"));
        var renamed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", TypeBody("اسم مُعدَّل"), etag);
        await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new { fields = new[] { Field("note") } }, renamed.Headers.ETag);

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-item-types/{typeId}/history", UriKind.Relative)));

        Assert.Equal(3, history.GetProperty("total").GetInt32());
        var items = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["update", "update", "insert"], items.Select(i => i.GetProperty("action").GetString()));
        Assert.All(items, i => Assert.Equal("سارة العتيبي", i.GetProperty("userName").GetString()));
        Assert.Equal("اسم مُعدَّل", items[1].GetProperty("after").GetProperty("name").GetString());
        Assert.Equal("نوع اختبار", items[1].GetProperty("before").GetProperty("name").GetString());
        Assert.Equal(JsonValueKindCompat.Null, items[2].GetProperty("before").ValueKind);

        Assert.Equal(HttpStatusCode.Forbidden, (await noura.GetAsync(new Uri($"/api/v1/work-item-types/{typeId}/history", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri($"/api/v1/work-item-types/{Guid.NewGuid()}/history", UriKind.Relative))).StatusCode);
    }
}

/// <summary>Short alias so the test reads "the before value of an insert is null".</summary>
internal static class JsonValueKindCompat
{
    public const System.Text.Json.JsonValueKind Null = System.Text.Json.JsonValueKind.Null;
}
