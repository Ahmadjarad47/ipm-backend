namespace Ipm.Domain.Config;

/// <summary>
/// One allowed move from <paramref name="FromStatusId"/> to <paramref name="ToStatusId"/>, with its conditions. The conditions
/// are enforced when the move is made (parts 04 to 06); this part only stores them.
/// </summary>
/// <param name="FromStatusId">The status the move starts from.</param>
/// <param name="ToStatusId">The status the move ends in.</param>
/// <param name="Permission">An extra permission beyond <c>workflow.transition</c>, if any.</param>
/// <param name="RequiresAssignee">The work item must have an assignee (work item workflows only).</param>
/// <param name="RequiresApproval">An approved request must exist for the move.</param>
/// <param name="BlockedByOpenDependencies">Refused while an open "blocks" dependency exists (work item workflows only).</param>
public sealed record WorkflowTransition(
    Guid FromStatusId,
    Guid ToStatusId,
    string? Permission,
    bool RequiresAssignee,
    bool RequiresApproval,
    bool BlockedByOpenDependencies);
