using Ipm.Domain.Config;

namespace Ipm.Domain.UnitTests.Config;

public sealed class StatusTests
{
    private static Status NewStatus(string key = "Done", StatusScope scope = StatusScope.WorkItem) =>
        Status.Create(key, " منجز ", " وصف ", "#2f6fb5", StatusCategory.Done, scope, 5, true).Value;

    [Fact]
    public void R1_the_key_is_normalized_and_the_text_is_trimmed()
    {
        var status = NewStatus("  Done ");

        Assert.Equal("done", status.Key);
        Assert.Equal("منجز", status.Name);
        Assert.Equal("وصف", status.Description);
        Assert.Equal(StatusScope.WorkItem, status.Scope);
        Assert.True(status.IsActive);
        Assert.Equal(7, status.Id.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("1done")]
    [InlineData("in-progress")]
    [InlineData("has space")]
    public void R1_a_malformed_key_is_refused(string key)
    {
        var result = Status.Create(key, "x", "", "#000000", StatusCategory.Active, StatusScope.Project, 0, true);

        Assert.Equal("Status.KeyInvalid", result.Error.Code);
    }

    [Fact]
    public void R1_and_R2_a_request_may_repeat_the_key_and_scope_but_never_change_them()
    {
        var status = NewStatus();

        Assert.True(status.EnsureKeyUnchanged(null).IsSuccess);
        Assert.True(status.EnsureKeyUnchanged("DONE").IsSuccess);
        Assert.Equal("Status.KeyImmutable", status.EnsureKeyUnchanged("finished").Error.Code);

        Assert.True(status.EnsureScopeUnchanged(null).IsSuccess);
        Assert.True(status.EnsureScopeUnchanged(StatusScope.WorkItem).IsSuccess);
        Assert.Equal("Status.ScopeImmutable", status.EnsureScopeUnchanged(StatusScope.Project).Error.Code);
    }

    [Fact]
    public void Update_changes_the_display_data_but_never_the_key_or_scope()
    {
        var status = NewStatus();

        status.Update(" مكتمل ", "", "#112233", StatusCategory.Cancelled, 9, false);

        Assert.Equal("مكتمل", status.Name);
        Assert.Equal(StatusCategory.Cancelled, status.Category);
        Assert.Equal(9, status.SortOrder);
        Assert.False(status.IsActive);
        Assert.Equal("done", status.Key);
        Assert.Equal(StatusScope.WorkItem, status.Scope);
    }

    [Theory]
    [InlineData("#2f6fb5", true)]
    [InlineData("2f6fb5", false)]
    [InlineData("#2f6fb", false)]
    public void R3_color_is_a_six_digit_hex(string color, bool valid) => Assert.Equal(valid, Status.IsValidColor(color));
}
