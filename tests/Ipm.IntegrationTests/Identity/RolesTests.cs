using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Identity;

/// <summary>Scenario 11 of docs/modules/01-identity.md (R15–R18) and the permission catalog.</summary>
[Collection(ApiCollection.Name)]
public sealed class RolesTests(ApiFactory factory)
{
    private static readonly Uri Roles = new("/api/v1/roles", UriKind.Relative);

    [Fact]
    public async Task The_catalog_lists_the_42_keys_in_ten_groups_with_arabic_labels()
    {
        using var admin = await factory.AdminClientAsync();

        var groups = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/permissions", UriKind.Relative)));

        Assert.Equal(10, groups.GetArrayLength());
        var keys = groups.EnumerateArray().SelectMany(g => g.GetProperty("permissions").EnumerateArray()).ToList();
        Assert.Equal(42, keys.Count);
        Assert.All(keys, k => Assert.Contains(k.GetProperty("label").GetString()!, c => c is >= '؀' and <= 'ۿ'));
    }

    [Fact]
    public async Task The_seven_built_in_roles_exist_with_their_scope_and_user_counts()
    {
        using var admin = await factory.AdminClientAsync();

        var page = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/roles?pageSize=200", UriKind.Relative)));
        var built = page.GetProperty("items").EnumerateArray().Where(r => r.GetProperty("isBuiltIn").GetBoolean()).ToList();

        Assert.Equal(7, built.Count);
        var staff = built.Single(r => r.GetProperty("key").GetString() == "staff");
        Assert.Equal("system", staff.GetProperty("scope").GetString());
        Assert.True(staff.GetProperty("userCount").GetInt32() >= 4);
        Assert.Equal("project", built.Single(r => r.GetProperty("key").GetString() == "viewer").GetProperty("scope").GetString());
    }

    [Fact]
    public async Task The_scope_filter_uses_kebab_case_and_rejects_unknown_values()
    {
        using var admin = await factory.AdminClientAsync();

        var projects = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/roles?scope=project&pageSize=200", UriKind.Relative)));
        Assert.All(projects.GetProperty("items").EnumerateArray(), r => Assert.Equal("project", r.GetProperty("scope").GetString()));

        var bad = await admin.GetAsync(new Uri("/api/v1/roles?scope=galaxy", UriKind.Relative));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Scenario_11_R15_a_built_in_role_cannot_be_deleted()
    {
        using var admin = await factory.AdminClientAsync();
        var id = await RoleIdAsync(admin, "viewer");
        var (_, etag) = await GetAsync(admin, $"/api/v1/roles/{id}");

        var response = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/roles/{id}", null, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Role.BuiltInCannotBeDeleted", (await ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task Scenario_11_R16_a_role_given_to_users_cannot_be_deleted_until_it_is_free()
    {
        using var admin = await factory.AdminClientAsync();
        var (roleId, etag) = await CreateRoleAsync(admin, Unique("inuse"), RoleScope.System, [Permissions.DashboardView]);
        await CreateUserAsync(admin, Unique("holder"), [roleId]);

        var blocked = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/roles/{roleId}", null, etag);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("Role.InUse", (await ProblemAsync(blocked)).Code);

        var (freeId, freeTag) = await CreateRoleAsync(admin, Unique("free"), RoleScope.System);
        var removed = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/roles/{freeId}", null, freeTag);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri($"/api/v1/roles/{freeId}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_11_R17_role_names_are_unique_whatever_the_letter_case()
    {
        using var admin = await factory.AdminClientAsync();
        var name = Unique("Dup");
        await CreateRoleAsync(admin, name, RoleScope.System);

        var again = await admin.PostAsJsonAsync(Roles, new { name = name.ToUpperInvariant(), scope = "system" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("Role.NameTaken", (await ProblemAsync(again)).Code);
    }

    [Fact]
    public async Task Scenario_11_R18_unknown_keys_and_a_grant_that_is_also_denied_are_refused()
    {
        using var admin = await factory.AdminClientAsync();

        var unknown = await admin.PostAsJsonAsync(Roles, new { name = Unique("bad"), scope = "system", permissions = new[] { "made.up" } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("Role.UnknownPermissions", (await ProblemAsync(unknown)).Code);

        var conflict = await admin.PostAsJsonAsync(
            Roles, new { name = Unique("bad"), scope = "system", permissions = new[] { Permissions.TasksView }, denied = new[] { Permissions.TasksView } });
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Equal("Role.PermissionConflict", (await ProblemAsync(conflict)).Code);
    }

    [Fact]
    public async Task Scenario_11_R15_the_scope_of_a_role_never_changes()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateRoleAsync(admin, Unique("scoped"), RoleScope.System);

        var response = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/roles/{id}",
            new { name = Unique("scoped"), description = "", permissions = Array.Empty<string>(), denied = Array.Empty<string>(), scope = "project" }, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Role.ImmutableField", (await ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task A_new_role_can_start_as_a_copy_of_another()
    {
        using var admin = await factory.AdminClientAsync();
        var staff = await RoleIdAsync(admin, "staff");

        var response = await admin.PostAsJsonAsync(Roles, new { name = Unique("copy"), scope = "system", copyFromRoleId = staff });
        var role = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("custom_", role.GetProperty("key").GetString(), StringComparison.Ordinal);
        Assert.False(role.GetProperty("isBuiltIn").GetBoolean());
        Assert.Equal([Permissions.DashboardView], role.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Scenario_13_a_role_edit_with_a_stale_etag_is_412_and_without_if_match_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateRoleAsync(admin, Unique("etag"), RoleScope.System);
        var body = new { name = Unique("etag"), description = "", permissions = Array.Empty<string>(), denied = Array.Empty<string>() };

        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/roles/{id}", body, etag);
        var second = await SendAsync(admin, HttpMethod.Put, $"/api/v1/roles/{id}", body, etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/roles/{id}", body, null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
    }
}
