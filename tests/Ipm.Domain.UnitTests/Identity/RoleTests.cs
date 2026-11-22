using Ipm.Domain.Identity;

namespace Ipm.Domain.UnitTests.Identity;

public sealed class RoleTests
{
    [Fact]
    public void Catalog_has_42_unique_known_keys_in_ten_groups()
    {
        Assert.Equal(42, Permissions.All.Count);
        Assert.Equal(42, Permissions.All.Distinct().Count());
        Assert.Equal(10, Permissions.Groups.Count);
        Assert.All(Permissions.All, key => Assert.True(Permissions.IsKnown(key)));
        Assert.False(Permissions.IsKnown("nope.view"));
    }

    [Fact]
    public void Create_generates_a_key_and_keeps_permissions_in_catalog_order()
    {
        var role = Role.Create("  فريق  ", " وصف ", RoleScope.System, [Permissions.TasksEdit, Permissions.ProjectsView], [Permissions.FilesDelete]).Value;

        Assert.StartsWith("custom_", role.Key, StringComparison.Ordinal);
        Assert.False(role.IsBuiltIn);
        Assert.Equal("فريق", role.Name);
        Assert.Equal([Permissions.ProjectsView, Permissions.TasksEdit], role.Granted);
        Assert.Equal([Permissions.FilesDelete], role.Denied);
    }

    [Fact]
    public void R18_unknown_keys_are_rejected()
    {
        var result = Role.Create("x", "", RoleScope.System, ["made.up"], []);

        Assert.True(result.IsFailure);
        Assert.Equal("Role.UnknownPermissions", result.Error.Code);
    }

    [Fact]
    public void R18_a_key_cannot_be_granted_and_denied_together()
    {
        var result = Role.Create("x", "", RoleScope.Project, [Permissions.TasksView], [Permissions.TasksView]);

        Assert.Equal("Role.PermissionConflict", result.Error.Code);
    }

    [Fact]
    public void Effective_permissions_leave_out_the_denied_ones()
    {
        var role = Role.Create("x", "", RoleScope.System, [Permissions.TasksView], []).Value;

        Assert.Equal([Permissions.TasksView], role.EffectivePermissions);
    }

    [Fact]
    public void R15_built_in_roles_cannot_be_deleted_but_custom_ones_can()
    {
        var builtIn = Role.CreateBuiltIn(Guid.CreateVersion7(), "staff", "موظف", "", RoleScope.System, [Permissions.DashboardView]);
        var custom = Role.Create("x", "", RoleScope.System, [], []).Value;

        Assert.Equal("Role.BuiltInCannotBeDeleted", builtIn.EnsureDeletable().Error.Code);
        Assert.True(custom.EnsureDeletable().IsSuccess);
    }

    [Fact]
    public void GrantMissing_completes_the_role_but_respects_its_denies()
    {
        var role = Role.CreateBuiltIn(Guid.CreateVersion7(), "system_admin", "مدير", "", RoleScope.System, [Permissions.ProjectsView]);
        Assert.True(role.SetPermissions([Permissions.ProjectsView], [Permissions.AuditExport]).IsSuccess);

        role.GrantMissing();

        Assert.Equal(41, role.Granted.Count);
        Assert.DoesNotContain(Permissions.AuditExport, role.Granted);
    }
}
