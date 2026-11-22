using System.Text.Json;
using Ipm.Domain.Config;
using Ipm.Domain.Work;

namespace Ipm.Domain.UnitTests.Work;

public sealed class WorkItemTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Type = Guid.NewGuid();
    private static readonly Guid Status = Guid.NewGuid();

    private static WorkItem NewItem(DateOnly? start = null, DateOnly? due = null) =>
        WorkItem.Create(Project, "ERP", 101, Type, Status, "  عنوان  ", " وصف ", Priority.High, start, due, null, [], null).Value;

    [Fact]
    public void R1_the_ref_is_the_project_code_and_number_and_text_is_trimmed()
    {
        var item = NewItem();

        Assert.Equal("ERP-101", item.Ref);
        Assert.Equal("عنوان", item.Title);
        Assert.Equal("وصف", item.Description);
        Assert.False(item.IsArchived);
    }

    [Fact]
    public void R1_the_due_date_cannot_precede_the_start_date()
    {
        var created = WorkItem.Create(
            Project, "ERP", 101, Type, Status, "x", "", Priority.Low, new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 1), null, [], null);

        Assert.Equal("WorkItem.InvalidDates", created.Error.Code);
        Assert.Equal("WorkItem.InvalidDates", NewItem(new DateOnly(2026, 5, 2)).SetDueDate(new DateOnly(2026, 5, 1)).Error.Code);
    }

    [Fact]
    public void R2_assignees_are_distinct_and_an_archived_item_is_not_editable()
    {
        var item = NewItem();
        var user = Guid.NewGuid();
        item.SetAssignees([user, user]);

        Assert.Single(item.AssigneeIds);
        Assert.True(item.EnsureEditable().IsSuccess);

        item.Archive();
        Assert.Equal("WorkItem.Archived", item.EnsureEditable().Error.Code);
    }

    [Fact]
    public void R5_field_values_are_checked_against_the_definitions_with_the_exact_path()
    {
        FieldDefinition[] definitions =
        [
            new("severity", "الخطورة", FieldType.Select, true, [new FieldOption("a", "أ")], null, null, null, false, false, 0),
            new("hours", "الساعات", FieldType.Number, false, null, 1, 10, null, false, false, 1),
            new("day", "اليوم", FieldType.Date, false, null, null, null, null, false, false, 2),
            new("flag", "علم", FieldType.Boolean, false, null, null, null, null, false, false, 3),
        ];

        Dictionary<string, JsonElement> Values(string json) => WorkItemFieldValues.Normalize(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json));

        Assert.Empty(WorkItemFieldValues.Validate(definitions, Values("""{"severity":"a","hours":5,"day":"2026-03-01","flag":true}""")));

        var missing = WorkItemFieldValues.Validate(definitions, Values("""{"hours":5}"""));
        Assert.Equal("fields.severity", Assert.Single(missing).Path);

        var bad = WorkItemFieldValues.Validate(definitions, Values("""{"severity":"z","hours":99,"day":"01/03/2026","flag":"yes","other":1}""")).Select(p => p.Path).ToList();
        Assert.Equal(["fields.day", "fields.flag", "fields.hours", "fields.other", "fields.severity"], bad.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void R5_null_and_empty_values_count_as_no_value_and_old_unknown_values_are_kept()
    {
        var normalized = WorkItemFieldValues.Normalize(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{"a":null,"b":"","c":"x"}"""));
        Assert.Equal(["c"], normalized.Keys);

        var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""{"old":"x"}""")!;
        Assert.Empty(WorkItemFieldValues.Validate([], WorkItemFieldValues.Normalize(existing), existing));
        Assert.Single(WorkItemFieldValues.Validate([], WorkItemFieldValues.Normalize(existing)));
    }
}
