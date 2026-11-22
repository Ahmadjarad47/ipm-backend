using Ipm.Domain.Common;

namespace Ipm.Domain.Approvals;

/// <summary>
/// A template of staged approval (FR-APR-01): which event it governs, the stages with their approvers, the fields it protects, and the
/// rule that selects the subjects it applies to. Stages and rule are columns of the template, so a change moves its version (ETag) and
/// shows in audit_log. Never deleted, only deactivated; running requests keep their own frozen copy of the stages (R1).
/// </summary>
public sealed class ApprovalTemplate : AuditableEntity
{
    private ApprovalTemplate() { }

    private ApprovalTemplate(Guid id) : base(id) { }

    public string Name { get; private set; } = string.Empty;

    public ApprovalEvent Event { get; private set; }

    public bool IsActive { get; private set; } = true;

    public List<ApprovalStage> Stages { get; private set; } = [];

    public List<string> ProtectedFields { get; private set; } = [];

    public ApprovalRule? Rule { get; private set; }

    public bool AllowSelfApproval { get; private set; }

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static Result<ApprovalTemplate> Create(
        string name, ApprovalEvent approvalEvent, bool isActive, IEnumerable<ApprovalStage> stages, IEnumerable<string> protectedFields,
        ApprovalRule? rule, bool allowSelfApproval, Guid? id = null)
    {
        var template = id is { } fixedId ? new ApprovalTemplate(fixedId) : new ApprovalTemplate();
        template.Event = approvalEvent;
        var defined = template.Update(name, isActive, stages, protectedFields, rule, allowSelfApproval);
        return defined.IsFailure ? defined.Error : template;
    }

    /// <summary>R1, R2: replaces the whole definition. The event never changes here (the handler checks the lock).</summary>
    public Result Update(
        string name, bool isActive, IEnumerable<ApprovalStage> stages, IEnumerable<string> protectedFields, ApprovalRule? rule, bool allowSelfApproval)
    {
        var wantedStages = stages.OrderBy(s => s.Order).ToList();
        var fields = protectedFields.ToList();
        if (ApprovalTemplateDefinition.Validate(name, Event, wantedStages, fields, rule).Count > 0)
        {
            return ApprovalErrors.TemplateInvalid;
        }

        Name = name.Trim();
        IsActive = isActive;
        Stages = wantedStages;
        ProtectedFields = fields;
        Rule = rule;
        AllowSelfApproval = allowSelfApproval;
        return Result.Success();
    }

    /// <summary>R2 of the spec: the handler refuses this once requests were made from the template.</summary>
    public void ChangeEvent(ApprovalEvent approvalEvent) => Event = approvalEvent;

    /// <summary>The stage after <paramref name="order"/>, or null when it was the last.</summary>
    public ApprovalStage? StageAfter(int order) => Stages.Find(s => s.Order == order + 1);
}
