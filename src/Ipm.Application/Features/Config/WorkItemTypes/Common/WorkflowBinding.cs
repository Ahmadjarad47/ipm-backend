using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Common;

/// <summary>R13 and R14: which workflow a work item type is bound to.</summary>
internal static class WorkflowBinding
{
    /// <summary>The key of the workflow a type gets when the request names none.</summary>
    public const string DefaultWorkflowKey = "standard";

    /// <summary>
    /// With a requested id: it must be an existing work item workflow (R13). Without one: the default <c>standard</c> workflow (R14),
    /// or an error when it does not exist.
    /// </summary>
    public static async Task<Result<Guid>> ResolveAsync(IApplicationDbContext db, Guid? requested, CancellationToken cancellationToken)
    {
        if (requested is { } id)
        {
            var valid = await db.Workflows.AsNoTracking().AnyAsync(w => w.Id == id && w.Scope == StatusScope.WorkItem, cancellationToken);
            return valid ? id : WorkItemTypeErrors.WorkflowInvalid;
        }

        var fallback = await db.Workflows.AsNoTracking()
            .Where(w => w.Key == DefaultWorkflowKey && w.Scope == StatusScope.WorkItem)
            .Select(w => (Guid?)w.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return fallback is { } defaultId ? defaultId : WorkItemTypeErrors.WorkflowRequired;
    }
}
