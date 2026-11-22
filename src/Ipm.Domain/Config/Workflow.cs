using System.Text.RegularExpressions;
using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

/// <summary>
/// A set of statuses and the moves allowed between them (FR-WF). Everything not listed as a transition is forbidden
/// (default deny). The <see cref="Key"/> and <see cref="Scope"/> never change (R2 R5). Statuses and transitions are columns of
/// the workflow itself, so changing them changes its version (ETag) and shows in audit_log. Never deleted (R4).
/// </summary>
public sealed partial class Workflow : AuditableEntity
{
    public const int KeyMaxLength = 31;
    public const int NameMaxLength = 100;

    private Workflow() { }

    private Workflow(Guid id) : base(id) { }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public StatusScope Scope { get; private set; }

    public List<Guid> StatusIds { get; private set; } = [];

    public Guid InitialStatusId { get; private set; }

    public List<WorkflowTransition> Transitions { get; private set; } = [];

    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    public static bool IsValidKey(string normalizedKey) => KeyPattern().IsMatch(normalizedKey);

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static Result<Workflow> Create(
        string key, string name, StatusScope scope, IEnumerable<Guid> statusIds, Guid initialStatusId, IEnumerable<WorkflowTransition> transitions, Guid? id = null)
    {
        var normalized = NormalizeKey(key);
        if (!IsValidKey(normalized))
        {
            return WorkflowErrors.KeyInvalid;
        }

        var workflow = id is { } fixedId ? new Workflow(fixedId) : new Workflow();
        workflow.Key = normalized;
        workflow.Scope = scope;
        var defined = workflow.SetDefinition(name, statusIds, initialStatusId, transitions);
        if (defined.IsFailure)
        {
            return defined.Error;
        }

        return workflow;
    }

    /// <summary>R6 to R10: replaces the name, the statuses and the transitions together. The workflow is unchanged when it fails.</summary>
    public Result SetDefinition(string name, IEnumerable<Guid> statusIds, Guid initialStatusId, IEnumerable<WorkflowTransition> transitions)
    {
        var statuses = statusIds.ToList();
        var moves = transitions.ToList();
        if (WorkflowDefinition.Validate(Scope, statuses, initialStatusId, moves).Count > 0)
        {
            return WorkflowErrors.DefinitionInvalid;
        }

        Name = name.Trim();
        StatusIds = statuses;
        InitialStatusId = initialStatusId;
        Transitions = moves;
        return Result.Success();
    }

    /// <summary>The move from one status to another, or null when the workflow does not allow it.</summary>
    public WorkflowTransition? FindTransition(Guid fromStatusId, Guid toStatusId) =>
        Transitions.Find(t => t.FromStatusId == fromStatusId && t.ToStatusId == toStatusId);

    /// <summary>R5: a request may repeat the key, but never a different one.</summary>
    public Result EnsureKeyUnchanged(string? requestedKey) =>
        requestedKey is null || NormalizeKey(requestedKey) == Key ? Result.Success() : WorkflowErrors.KeyImmutable;

    /// <summary>R2: a request may repeat the scope, but never a different one.</summary>
    public Result EnsureScopeUnchanged(StatusScope? requestedScope) =>
        requestedScope is null || requestedScope == Scope ? Result.Success() : WorkflowErrors.ScopeImmutable;

    [GeneratedRegex("^[a-z][a-z0-9_]{1,30}$")]
    private static partial Regex KeyPattern();
}
