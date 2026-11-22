using Ipm.Domain.Common;
using Ipm.Domain.Projects;

namespace Ipm.Domain.UnitTests.Projects;

public sealed class ProjectTemplateTests
{
    private static readonly Guid TypeA = Guid.CreateVersion7();
    private static readonly Guid TypeB = Guid.CreateVersion7();
    private static readonly Guid Initial = Guid.CreateVersion7();
    private static readonly Guid Role = Guid.CreateVersion7();

    private static Result<ProjectTemplate> Make(Guid[]? allowed = null, Guid? defaultType = null, int days = 30) =>
        ProjectTemplate.Create(" قالب ", " وصف ", true, allowed ?? [TypeA, TypeB], defaultType ?? TypeA, Initial, [Role, Role], days, [new SuggestedMember(null, Role)]);

    [Fact]
    public void A_valid_template_keeps_its_presets_and_drops_duplicates()
    {
        var template = Make().Value;

        Assert.Equal("قالب", template.Name);
        Assert.Equal([TypeA, TypeB], template.AllowedTypeIds);
        Assert.Equal([Role], template.SuggestedRoleIds);
        Assert.Equal(Initial, template.InitialStatusId);
        Assert.Equal(30, template.DurationDays);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void R14_at_least_one_type_and_the_default_among_them()
    {
        Assert.Equal("ProjectTemplate.InvalidTypes", Make(allowed: []).Error.Code);
        Assert.Equal("ProjectTemplate.InvalidTypes", Make(allowed: [TypeA], defaultType: TypeB).Error.Code);
    }

    [Fact]
    public void R14_the_duration_is_at_least_one_day()
    {
        Assert.Equal("ProjectTemplate.InvalidDuration", Make(days: 0).Error.Code);
        Assert.True(Make(days: 1).IsSuccess);
    }

    [Fact]
    public void A_refused_update_leaves_the_template_unchanged()
    {
        var template = Make().Value;

        var result = template.Update("جديد", "", false, [TypeA], TypeA, Initial, [], 0, []);

        Assert.True(result.IsFailure);
        Assert.Equal("قالب", template.Name);
        Assert.True(template.IsActive);
    }
}
