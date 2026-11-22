using Ipm.Domain.Projects;

namespace Ipm.Domain.UnitTests.Projects;

public sealed class ProjectTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();
    private static readonly Guid TypeA = Guid.CreateVersion7();
    private static readonly Guid TypeB = Guid.CreateVersion7();

    private static Project NewProject(string code = "erp") =>
        Project.Create(code, " تطوير ", " وصف ", Owner, Guid.CreateVersion7(), new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), [TypeA, TypeB], TypeA).Value;

    private static Project WithMembers()
    {
        var project = NewProject();
        Assert.True(project.SetMembers([(Owner, Manager), (Member, Member)]).IsSuccess);
        return project;
    }

    [Fact]
    public void R1_the_code_is_normalized_to_upper_case_and_the_text_is_trimmed()
    {
        var project = NewProject(" erp ");

        Assert.Equal("ERP", project.Code);
        Assert.Equal("تطوير", project.Name);
        Assert.Equal("وصف", project.Description);
        Assert.False(project.IsArchived);
        Assert.Equal(7, project.Id.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1ERP")]
    [InlineData("ERP-1")]
    [InlineData("ABCDEFGHIJK")]
    public void R1_a_malformed_code_is_refused(string code)
    {
        var result = Project.Create(code, "x", "", Owner, Guid.CreateVersion7(), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), [TypeA], TypeA);

        Assert.Equal("Project.CodeInvalid", result.Error.Code);
    }

    [Fact]
    public void R1_a_request_may_repeat_the_code_but_never_change_it()
    {
        var project = NewProject();

        Assert.True(project.EnsureCodeUnchanged(null).IsSuccess);
        Assert.True(project.EnsureCodeUnchanged("erp").IsSuccess);
        Assert.Equal("Project.CodeImmutable", project.EnsureCodeUnchanged("OTHER").Error.Code);
    }

    [Fact]
    public void R2_the_end_date_cannot_precede_the_start_date()
    {
        var project = NewProject();

        Assert.Equal("Project.InvalidDates", project.Update("x", "", new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1)).Error.Code);
        Assert.True(project.Update("x", "", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 1)).IsSuccess);
        Assert.Equal(
            "Project.InvalidDates",
            Project.Create("ERP", "x", "", Owner, Guid.CreateVersion7(), new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1), [TypeA], TypeA).Error.Code);
    }

    [Fact]
    public void R6_an_archived_project_is_not_editable_until_it_is_restored()
    {
        var project = NewProject();

        project.Archive();
        Assert.Equal("Project.Archived", project.EnsureEditable().Error.Code);

        project.Unarchive();
        Assert.True(project.EnsureEditable().IsSuccess);
    }

    [Fact]
    public void R7_members_must_be_unique_and_include_the_owner()
    {
        var project = NewProject();

        Assert.Equal("Project.MembersInvalid", project.SetMembers([(Owner, Manager), (Owner, Member)]).Error.Code);
        Assert.Equal("Project.OwnerMustBeMember", project.SetMembers([(Member, Member)]).Error.Code);
        Assert.Empty(project.Members);
        Assert.Equal(0, project.MembersRevision);
    }

    [Fact]
    public void Members_are_added_re_roled_and_removed_and_each_change_moves_the_revision()
    {
        var project = WithMembers();
        Assert.Equal(1, project.MembersRevision);
        Assert.Equal(2, project.Members.Count);
        var memberRow = project.Members.Single(m => m.UserId == Member);

        var other = Guid.CreateVersion7();
        Assert.True(project.SetMembers([(Owner, Manager), (Member, other)]).IsSuccess);
        Assert.Equal(2, project.MembersRevision);
        Assert.Equal(other, project.Members.Single(m => m.UserId == Member).RoleId);
        Assert.Same(memberRow, project.Members.Single(m => m.UserId == Member));

        Assert.True(project.SetMembers([(Owner, Manager)]).IsSuccess);
        Assert.Equal(3, project.MembersRevision);
        Assert.Single(project.Members);
    }

    [Fact]
    public void Setting_the_same_members_again_leaves_the_revision_alone()
    {
        var project = WithMembers();

        Assert.True(project.SetMembers([(Member, Member), (Owner, Manager)]).IsSuccess);

        Assert.Equal(1, project.MembersRevision);
    }

    [Fact]
    public void R3_the_owner_can_only_move_to_an_existing_member()
    {
        var project = WithMembers();

        Assert.Equal("Project.OwnerMustBeMember", project.ChangeOwner(Guid.CreateVersion7()).Error.Code);
        Assert.True(project.ChangeOwner(Member).IsSuccess);
        Assert.Equal(Member, project.OwnerId);
        Assert.True(project.ChangeOwner(Member).IsSuccess);
    }

    [Fact]
    public void R12_at_least_one_type_and_the_default_among_them()
    {
        var project = NewProject();

        Assert.Equal("Project.TypesInvalid", project.SetAllowedTypes([], TypeA).Error.Code);
        Assert.Equal("Project.TypesInvalid", project.SetAllowedTypes([TypeA], TypeB).Error.Code);
        Assert.True(project.SetAllowedTypes([TypeB, TypeB], TypeB).IsSuccess);
        Assert.Equal([TypeB], project.AllowedTypeIds);
        Assert.Equal(TypeB, project.DefaultTypeId);
    }

    [Fact]
    public void R13_one_workflow_override_per_type_and_the_effective_workflow_prefers_it()
    {
        var project = NewProject();
        var own = Guid.CreateVersion7();
        var bug = Guid.CreateVersion7();

        project.SetWorkflowOverrides([new WorkflowOverride(TypeA, own), new WorkflowOverride(TypeA, bug)]);

        Assert.Single(project.WorkflowOverrides);
        Assert.Equal(bug, project.EffectiveWorkflowId(TypeA, own));
        Assert.Equal(own, project.EffectiveWorkflowId(TypeB, own));
    }

    [Fact]
    public void R9_moving_the_status_replaces_it()
    {
        var project = NewProject();
        var next = Guid.CreateVersion7();

        project.MoveTo(next);

        Assert.Equal(next, project.StatusId);
    }
}
