using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common.Exceptions;
using Ipm.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Common;

/// <summary>R1 and R2: the template rules, reported with the exact path of every problem like the validators do.</summary>
internal static class ApprovalTemplateChecks
{
    /// <summary>
    /// The structural problems of the domain, and (for an active template) the ones that need the database: the user is active, the role
    /// exists, and the projects and types of the rule exist. Throws a validation error with every path.
    /// </summary>
    public static async Task EnsureValidAsync(
        IApplicationDbContext db, string name, ApprovalEvent approvalEvent, bool active, IReadOnlyList<ApprovalStage> stages,
        IReadOnlyList<string> protectedFields, ApprovalRule? rule, CancellationToken cancellationToken)
    {
        var ordered = stages.OrderBy(s => s.Order).ToList();
        var problems = ApprovalTemplateDefinition.Validate(name, approvalEvent, ordered, protectedFields, rule).ToList();

        if (active && problems.Count == 0)
        {
            problems.AddRange(await StageProblemsAsync(db, ordered, cancellationToken));
            problems.AddRange(await RuleProblemsAsync(db, rule, cancellationToken));
        }

        if (problems.Count > 0)
        {
            throw new ValidationException(problems.GroupBy(p => p.Path, p => p.Message).ToDictionary(g => g.Key, g => g.Distinct().ToArray()));
        }
    }

    private static async Task<List<(string, string)>> StageProblemsAsync(IApplicationDbContext db, List<ApprovalStage> stages, CancellationToken cancellationToken)
    {
        var problems = new List<(string, string)>();
        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            var at = $"stages[{i}].approverId";
            if (stage.ApproverKind == ApproverKind.User)
            {
                var id = Guid.Parse(stage.ApproverId);
                var active = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => (bool?)u.IsActive).SingleOrDefaultAsync(cancellationToken);
                if (active is null)
                {
                    problems.Add((at, "المعتمد غير موجود."));
                }
                else if (active == false)
                {
                    problems.Add((at, "المعتمد غير نشط."));
                }
            }
            else if (stage.ApproverKind == ApproverKind.Role && !await db.Roles.AsNoTracking().AnyAsync(r => r.Id == Guid.Parse(stage.ApproverId), cancellationToken))
            {
                problems.Add((at, "الدور غير موجود."));
            }
        }

        return problems;
    }

    private static async Task<List<(string, string)>> RuleProblemsAsync(IApplicationDbContext db, ApprovalRule? rule, CancellationToken cancellationToken)
    {
        var problems = new List<(string, string)>();
        if (rule?.ProjectIds is { Count: > 0 } projects
            && await db.Projects.AsNoTracking().CountAsync(p => projects.Contains(p.Id), cancellationToken) != projects.Distinct().Count())
        {
            problems.Add(("rule.projectIds", "مشروع غير معروف في القاعدة."));
        }

        if (rule?.WorkItemTypeIds is { Count: > 0 } types
            && await db.WorkItemTypes.AsNoTracking().CountAsync(t => types.Contains(t.Id), cancellationToken) != types.Distinct().Count())
        {
            problems.Add(("rule.workItemTypeIds", "نوع عمل غير معروف في القاعدة."));
        }

        return problems;
    }
}
