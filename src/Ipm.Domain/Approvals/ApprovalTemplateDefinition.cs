namespace Ipm.Domain.Approvals;

/// <summary>
/// The structural rules of a template (R1, R2) in one place: the validator reports every problem with its path, and the entity
/// refuses the definition again as a last line of defense. Rules that need the database (the user or role exists, is active) are the handler job.
/// </summary>
public static class ApprovalTemplateDefinition
{
    public const int NameMaxLength = 150;
    public const int StageNameMaxLength = 100;
    public const int MaxStages = 10;
    public static readonly string[] Operators = ["eq", "neq", "gt", "lt", "in"];

    public static IReadOnlyList<(string Path, string Message)> Validate(
        string name, ApprovalEvent approvalEvent, IReadOnlyList<ApprovalStage> stages, IReadOnlyList<string> protectedFields, ApprovalRule? rule)
    {
        var problems = new List<(string, string)>();

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            problems.Add(("name", $"الاسم مطلوب ولا يزيد على {NameMaxLength} حرفاً."));
        }

        if (!Enum.IsDefined(approvalEvent))
        {
            problems.Add(("event", "حدث غير معروف."));
        }
        else if (approvalEvent == ApprovalEvent.SensitiveChange)
        {
            if (ApprovalSubjectFields.SubjectOf(approvalEvent, protectedFields) is null)
            {
                problems.Add(("protectedFields", "التعديل الحساس يتطلب حقولاً محمية صالحة لنوع واحد (مشروع أو عنصر عمل)."));
            }
        }
        else
        {
            var subject = ApprovalSubjectFields.SubjectOf(approvalEvent, protectedFields)!.Value;
            if (protectedFields.Any(f => !ApprovalSubjectFields.For(subject).Contains(f)))
            {
                problems.Add(("protectedFields", "حقول محمية غير معروفة لهذا الحدث."));
            }
        }

        if (protectedFields.Distinct().Count() != protectedFields.Count)
        {
            problems.Add(("protectedFields", "توجد حقول مكررة."));
        }

        CheckStages(stages, problems);
        CheckRule(rule, problems);
        return problems;
    }

    private static void CheckStages(IReadOnlyList<ApprovalStage> stages, List<(string, string)> problems)
    {
        if (stages.Count == 0 || stages.Count > MaxStages)
        {
            problems.Add(("stages", $"من مرحلة إلى {MaxStages} مراحل."));
            return;
        }

        var ordered = stages.OrderBy(s => s.Order).ToList();
        if (ordered.Select((s, i) => s.Order == i + 1).Any(ok => !ok))
        {
            problems.Add(("stages", "ترتيب المراحل يجب أن يكون متسلسلاً وفريداً يبدأ من 1."));
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var at = $"stages[{i}]";
            if (string.IsNullOrWhiteSpace(s.Name) || s.Name.Trim().Length > StageNameMaxLength)
            {
                problems.Add(($"{at}.name", "اسم المرحلة مطلوب."));
            }

            if (s.DueHours is { } hours && (hours < 1 || hours > 720))
            {
                problems.Add(($"{at}.dueHours", "المهلة بين 1 و720 ساعة."));
            }

            if (!Enum.IsDefined(s.ApproverKind) || string.IsNullOrWhiteSpace(s.ApproverId))
            {
                problems.Add(($"{at}.approverId", "المعتمد مطلوب."));
            }
            else if (s.ApproverKind == ApproverKind.Position && !TryParsePosition(s.ApproverId, out _))
            {
                problems.Add(($"{at}.approverId", "منصب غير صالح (project-owner أو direct-manager أو project-manager)."));
            }
            else if (s.ApproverKind != ApproverKind.Position && !Guid.TryParse(s.ApproverId, out _))
            {
                problems.Add(($"{at}.approverId", "معرّف المعتمد غير صالح."));
            }

            if (i > 0 && ordered[i - 1].ApproverKind == s.ApproverKind && ordered[i - 1].ApproverId == s.ApproverId)
            {
                problems.Add(($"{at}.approverId", "المعتمد نفسه في مرحلتين متتاليتين."));
            }
        }
    }

    private static void CheckRule(ApprovalRule? rule, List<(string, string)> problems)
    {
        if (rule is null)
        {
            return;
        }

        if (rule.Actions?.Any(a => !Enum.IsDefined(a)) == true)
        {
            problems.Add(("rule.actions", "إجراء غير صالح في القاعدة."));
        }

        if (rule.Priorities?.Any(p => !Enum.IsDefined(p)) == true)
        {
            problems.Add(("rule.priorities", "أولوية غير صالحة في القاعدة."));
        }

        foreach (var (condition, index) in (rule.Conditions ?? []).Select((c, i) => (c, i)))
        {
            var isList = condition.Value.ValueKind == System.Text.Json.JsonValueKind.Array;
            if (string.IsNullOrWhiteSpace(condition.Field) || !Operators.Contains(condition.Op) || (condition.Op == "in") != isList)
            {
                problems.Add(($"rule.conditions[{index}]", "شرط غير صالح في القاعدة."));
            }
        }
    }

    public static bool TryParsePosition(string text, out ApproverPosition position)
    {
        position = text switch
        {
            "project-owner" => ApproverPosition.ProjectOwner,
            "direct-manager" => ApproverPosition.DirectManager,
            _ => ApproverPosition.ProjectManager,
        };
        return text is "project-owner" or "direct-manager" or "project-manager";
    }
}
