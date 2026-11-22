using System.Text.Json;
using Ipm.Domain.Work;

namespace Ipm.Domain.Approvals;

/// <summary>One stage of a template. <paramref name="ApproverId"/> is a user id, a role id or a position name, by <paramref name="ApproverKind"/>.</summary>
public sealed record ApprovalStage(int Order, string Name, ApproverKind ApproverKind, string ApproverId, int? DueHours);

/// <summary>A condition on a field of the subject: <c>eq</c>, <c>neq</c>, <c>gt</c>, <c>lt</c> or <c>in</c>.</summary>
public sealed record RuleCondition(string Field, string Op, JsonElement Value);

/// <summary>Selects which subjects and actions a template governs; every part is optional and a missing part matches all (R2).</summary>
public sealed record ApprovalRule(
    IReadOnlyList<Guid>? WorkItemTypeIds,
    IReadOnlyList<Guid>? ProjectIds,
    IReadOnlyList<Priority>? Priorities,
    IReadOnlyList<ApprovalAction>? Actions,
    IReadOnlyList<RuleCondition>? Conditions,
    int? Priority);

/// <summary>One decision on one stage of one revision (R6). <paramref name="OnBehalfOf"/> is set when a delegate decided.</summary>
public sealed record ApprovalDecision(int StageOrder, Guid DeciderId, DecisionKind Decision, string Comment, DateTimeOffset At, int Revision, Guid? OnBehalfOf);

/// <summary>The direct approvers of a stage, resolved once when the request is submitted (R5).</summary>
public sealed record ResolvedApprovers(int Order, IReadOnlyList<Guid> UserIds);

/// <summary>The frozen value of one protected field when the request was submitted.</summary>
public sealed record SnapshotEntry(string Field, string? Value);
