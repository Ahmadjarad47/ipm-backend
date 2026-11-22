using Ipm.Domain.Identity;

namespace Ipm.Domain.UnitTests.Identity;

public sealed class UserTests
{
    private static User NewUser(params Guid[] roles) =>
        User.Create(" Khaled ", "KHALED", " خالد ", " k@x.io ", "K@X.IO", " مدير ", null, roles);

    [Fact]
    public void Create_normalizes_the_text_fields_and_lowercases_the_username()
    {
        var user = NewUser();

        Assert.Equal("khaled", user.UserName);
        Assert.Equal("خالد", user.Name);
        Assert.Equal("k@x.io", user.Email);
        Assert.Equal("مدير", user.Title);
        Assert.True(user.IsActive);
        Assert.Equal(7, user.Id.Version);
        Assert.NotNull(user.SecurityStamp);
    }

    [Fact]
    public void R9_a_user_cannot_deactivate_themselves()
    {
        var user = NewUser();

        var self = user.Deactivate(user.Id);
        var other = user.Deactivate(Guid.CreateVersion7());

        Assert.Equal("User.CannotDeactivateSelf", self.Error.Code);
        Assert.True(other.IsSuccess);
        Assert.False(user.IsActive);
    }

    [Fact]
    public void R10_a_user_cannot_change_their_own_roles()
    {
        var role = Guid.CreateVersion7();
        var user = NewUser();

        Assert.Equal("User.CannotChangeOwnRoles", user.ChangeRoles(user.Id, [role]).Error.Code);
        Assert.Empty(user.RoleIds);

        Assert.True(user.ChangeRoles(Guid.CreateVersion7(), [role, role]).IsSuccess);
        Assert.Equal([role], user.RoleIds);
    }

    [Fact]
    public void R6_a_password_set_by_an_administrator_must_be_changed_by_the_owner()
    {
        var user = NewUser();

        user.PasswordSetByAdministrator();
        Assert.True(user.MustChangePassword);

        user.PasswordChangedByOwner();
        Assert.False(user.MustChangePassword);
    }
}
