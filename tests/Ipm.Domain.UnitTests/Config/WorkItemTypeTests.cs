using Ipm.Domain.Config;

namespace Ipm.Domain.UnitTests.Config;

public sealed class WorkItemTypeTests
{
    private static WorkItemType NewType(string key = "Bug") =>
        WorkItemType.Create(key, " خطأ ", " وصف ", "#c0392b", WorkItemIcon.Bug, 3, true).Value;

    private static FieldDefinition Field(string key, FieldType type = FieldType.Text, int order = 0, IReadOnlyList<FieldOption>? options = null) =>
        new(key, "حقل", type, false, options, null, null, null, false, false, order);

    [Fact]
    public void R1_the_key_is_normalized_to_lower_case_and_trimmed_values_are_kept()
    {
        var type = NewType("  Bug ");

        Assert.Equal("bug", type.Key);
        Assert.Equal("خطأ", type.Name);
        Assert.Equal("وصف", type.Description);
        Assert.Equal(7, type.Id.Version);
        Assert.True(type.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("1bug")]
    [InlineData("bug-x")]
    [InlineData("has space")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789")]
    public void R1_a_malformed_key_is_refused(string key)
    {
        var result = WorkItemType.Create(key, "x", "", "#000000", WorkItemIcon.Task, 0, true);

        Assert.Equal("WorkItemType.KeyInvalid", result.Error.Code);
    }

    [Fact]
    public void R1_the_key_can_be_repeated_but_never_changed()
    {
        var type = NewType();

        Assert.True(type.EnsureKeyUnchanged(null).IsSuccess);
        Assert.True(type.EnsureKeyUnchanged("BUG").IsSuccess);
        Assert.Equal("WorkItemType.KeyImmutable", type.EnsureKeyUnchanged("defect").Error.Code);
    }

    [Theory]
    [InlineData("#c0392b", true)]
    [InlineData("#C0392B", true)]
    [InlineData("c0392b", false)]
    [InlineData("#c0392", false)]
    [InlineData("red", false)]
    public void R2_color_is_a_six_digit_hex(string color, bool valid) => Assert.Equal(valid, WorkItemType.IsValidColor(color));

    [Fact]
    public void R3_valid_fields_are_stored_ordered_by_their_display_order()
    {
        var type = NewType();

        var result = type.SetFields([Field("second", order: 2), Field("first", order: 1)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(["first", "second"], type.Fields.Select(f => f.Key));
    }

    [Fact]
    public void R3_each_problem_is_reported_with_its_exact_path()
    {
        var problems = FieldDefinitions.Validate(
        [
            Field("ok"),
            Field("ok"),
            Field("Bad-Key"),
            Field("pick", FieldType.Select),
            Field("dupe_options", FieldType.Select, options: [new FieldOption("a", "أ"), new FieldOption("a", "ب")]),
            Field("range") with { Min = 10, Max = 1 },
        ]);

        var paths = problems.Select(p => p.Path).ToList();
        Assert.Contains("fields[1].key", paths);
        Assert.Contains("fields[2].key", paths);
        Assert.Contains("fields[3].options", paths);
        Assert.Contains("fields[4].options", paths);
        Assert.Contains("fields[5].max", paths);
        Assert.All(problems, p => Assert.Contains(p.Message, c => c is >= '؀' and <= 'ۿ'));
    }

    [Fact]
    public void R3_more_than_forty_fields_are_refused()
    {
        var fields = Enumerable.Range(0, FieldDefinitions.MaxFields + 1).Select(i => Field($"f{i}")).ToList();

        Assert.Contains(FieldDefinitions.Validate(fields), p => p.Path == "fields");
        Assert.Equal("WorkItemType.InvalidFields", NewType().SetFields(fields).Error.Code);
    }

    [Fact]
    public void R3_a_select_field_needs_its_options_but_other_types_ignore_them()
    {
        Assert.Empty(FieldDefinitions.Validate([Field("pick", FieldType.Select, options: [new FieldOption("a", "أ")])]));
        Assert.Empty(FieldDefinitions.Validate([Field("note", FieldType.Text)]));
        Assert.NotEmpty(FieldDefinitions.Validate([Field("pick", FieldType.Select)]));
    }

    [Fact]
    public void R5_children_are_deduplicated_and_may_include_the_type_itself()
    {
        var type = NewType();
        var other = Guid.CreateVersion7();

        type.SetAllowedChildren([other, type.Id, other]);

        Assert.Equal([other, type.Id], type.AllowedChildTypeIds);
    }
}
