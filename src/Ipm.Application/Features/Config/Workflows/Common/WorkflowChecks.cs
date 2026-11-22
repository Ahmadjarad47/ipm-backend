using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common.Exceptions;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Workflows.Common;

/// <summary>Checks shared by the create and update commands of a workflow.</summary>
internal static class WorkflowChecks
{
    /// <summary>
    /// R6 to R10 as one 400 with the exact path of every problem (<c>transitions[2].toStatusId</c>), the same way the
    /// FluentValidation validators report. It runs in the handler because the rules of an update depend on the stored scope.
    /// </summary>
    public static void EnsureDefinitionValid(
        StatusScope scope, IReadOnlyList<Guid> statusIds, Guid initialStatusId, IReadOnlyList<WorkflowTransition> transitions)
    {
        var problems = WorkflowDefinition.Validate(scope, statusIds, initialStatusId, transitions);
        if (problems.Count == 0)
        {
            return;
        }

        throw new ValidationException(problems
            .GroupBy(p => p.Path, p => p.Message)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray()));
    }

    /// <summary>
    /// R6 and R12: every status exists and has the workflow scope, and a status that is not already in the workflow must be active
    /// (a status switched off stays in the workflows that already have it, so nothing breaks).
    /// </summary>
    public static async Task<Result> EnsureStatusesAsync(
        IApplicationDbContext db,
        StatusScope scope,
        IReadOnlyCollection<Guid> statusIds,
        IReadOnlyCollection<Guid> alreadyInWorkflow,
        CancellationToken cancellationToken)
    {
        var wanted = statusIds.Distinct().ToList();
        var statuses = await db.Statuses.AsNoTracking().Where(s => wanted.Contains(s.Id)).ToListAsync(cancellationToken);

        var valid = statuses.Count == wanted.Count
            && statuses.All(s => s.Scope == scope && (s.IsActive || alreadyInWorkflow.Contains(s.Id)));
        return valid ? Result.Success() : WorkflowErrors.StatusesInvalid;
    }
}
