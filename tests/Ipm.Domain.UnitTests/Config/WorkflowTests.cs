using Ipm.Domain.Config;
using Ipm.Domain.Identity;

namespace Ipm.Domain.UnitTests.Config;

public sealed class WorkflowTests
{
    private static readonly Guid A = Guid.CreateVersion7();
    private static readonly Guid B = Guid.CreateVersion7();
    private static readonly Guid C = Guid.CreateVersion7();

    private static WorkflowTransition Move(Guid from, Guid to, string? permission = null, bool assignee = false, bool approval = false, bool deps = false) =>
        new(from, to, permission, assignee, approval, deps);

    private static Workflow NewWorkflow(StatusScope scope = StatusScope.WorkItem) =>
        Workflow.Create("Standard", " قياسي ", scope, [A, B, C], A, [Move(A, B), Move(B, C)]).Value;

    [Fact]
    public void Create_normalizes_the_key_and_keeps_the_definition()
    {
        var workflow = NewWorkflow();

        Assert.Equal("standard", workflow.Key);
        Assert.Equal("قياسي", workflow.Name);
        Assert.Equal([A, B, C], workflow.StatusIds);
        Assert.Equal(A, workflow.InitialStatusId);
        Assert.Equal(2, workflow.Transitions.Count);
    }

    [Fact]
    public void R5_a_malformed_key_is_refused_and_the_key_and_scope_never_change()
    {
        Assert.Equal("Workflow.KeyInvalid", Workflow.Create("1x", "x", StatusScope.WorkItem, [A], A, []).Error.Code);

        var workflow = NewWorkflow();
        Assert.True(workflow.EnsureKeyUnchanged("STANDARD").IsSuccess);
        Assert.Equal("Workflow.KeyImmutable", workflow.EnsureKeyUnchanged("other").Error.Code);
        Assert.True(workflow.EnsureScopeUnchanged(StatusScope.WorkItem).IsSuccess);
        Assert.Equal("Workflow.ScopeImmutable", workflow.EnsureScopeUnchanged(StatusScope.Project).Error.Code);
    }

    [Fact]
    public void R6_R7_statuses_must_be_present_unique_and_contain_the_initial_one()
    {
        Assert.Contains(WorkflowDefinition.Validate(StatusScope.WorkItem, [], A, []), p => p.Path == "statusIds");
        Assert.Contains(WorkflowDefinition.Validate(StatusScope.WorkItem, [A, A], A, []), p => p.Path == "statusIds");
        Assert.Contains(WorkflowDefinition.Validate(StatusScope.WorkItem, [A, B], C, []), p => p.Path == "initialStatusId");
        Assert.Empty(WorkflowDefinition.Validate(StatusScope.WorkItem, [A, B], B, []));
    }

    [Fact]
    public void R8_each_transition_problem_is_reported_with_its_exact_path()
    {
        var problems = WorkflowDefinition.Validate(
            StatusScope.WorkItem,
            [A, B],
            A,
            [Move(A, B), Move(A, B), Move(A, A), Move(A, C), Move(C, B)]);

        var paths = problems.Select(p => p.Path).ToList();
        Assert.Contains("transitions[1]", paths);
        Assert.Contains("transitions[2].toStatusId", paths);
        Assert.Contains("transitions[3].toStatusId", paths);
        Assert.Contains("transitions[4].fromStatusId", paths);
        Assert.All(problems, p => Assert.Contains(p.Message, c => c is >= '؀' and <= 'ۿ'));
    }

    [Fact]
    public void R9_an_unknown_permission_is_refused_and_a_known_one_is_accepted()
    {
        Assert.Contains(WorkflowDefinition.Validate(StatusScope.WorkItem, [A, B], A, [Move(A, B, "made.up")]), p => p.Path == "transitions[0].permission");
        Assert.Empty(WorkflowDefinition.Validate(StatusScope.WorkItem, [A, B], A, [Move(A, B, Permissions.TasksEdit)]));
    }

    [Fact]
    public void R10_assignee_and_dependency_conditions_belong_to_work_item_workflows_only()
    {
        var problems = WorkflowDefinition.Validate(StatusScope.Project, [A, B], A, [Move(A, B, assignee: true, deps: true)]);

        Assert.Contains(problems, p => p.Path == "transitions[0].requiresAssignee");
        Assert.Contains(problems, p => p.Path == "transitions[0].blockedByOpenDependencies");
        Assert.Empty(WorkflowDefinition.Validate(StatusScope.Project, [A, B], A, [Move(A, B, approval: true)]));
        Assert.Empty(WorkflowDefinition.Validate(StatusScope.WorkItem, [A, B], A, [Move(A, B, assignee: true, deps: true)]));
    }

    [Fact]
    public void SetDefinition_leaves_the_workflow_unchanged_when_it_is_refused()
    {
        var workflow = NewWorkflow();

        var result = workflow.SetDefinition("جديد", [A, B], A, [Move(A, A)]);

        Assert.Equal("Workflow.DefinitionInvalid", result.Error.Code);
        Assert.Equal("قياسي", workflow.Name);
        Assert.Equal([A, B, C], workflow.StatusIds);
        Assert.Equal(2, workflow.Transitions.Count);
    }

    [Fact]
    public void FindTransition_returns_only_the_listed_moves()
    {
        var workflow = NewWorkflow();

        Assert.NotNull(workflow.FindTransition(A, B));
        Assert.Null(workflow.FindTransition(B, A));
        Assert.Null(workflow.FindTransition(A, C));
    }
}
