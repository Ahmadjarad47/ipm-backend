using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

public static class WorkflowErrors
{
    public static readonly Error NotFound = Error.NotFound("Workflow.NotFound", "سير العمل غير موجود.");

    /// <summary>R5.</summary>
    public static readonly Error KeyTaken = Error.Conflict("Workflow.KeyTaken", "يوجد سير عمل بنفس المفتاح.");

    /// <summary>R5.</summary>
    public static readonly Error KeyInvalid = Error.Validation(
        "Workflow.KeyInvalid", "المفتاح: أحرف إنجليزية وأرقام و_ (من 2 إلى 31 خانة) ويبدأ بحرف.");

    /// <summary>R5.</summary>
    public static readonly Error KeyImmutable = Error.Failure(
        "Workflow.KeyImmutable", "لا يمكن تغيير مفتاح سير العمل بعد إنشائه. غيّر الاسم بدلاً منه.");

    /// <summary>R2.</summary>
    public static readonly Error ScopeImmutable = Error.Failure(
        "Workflow.ScopeImmutable", "لا يمكن تغيير نطاق سير العمل بعد إنشائه.");

    /// <summary>R6 to R10. The exact problems are reported by the validator; this is the domain last line of defense.</summary>
    public static readonly Error DefinitionInvalid = Error.Validation(
        "Workflow.DefinitionInvalid", "تعريف سير العمل غير صالح: الحالات أو الحالة الابتدائية أو الانتقالات.");

    /// <summary>R6 and R12: the statuses exist, have this scope, and a newly added one is active.</summary>
    public static readonly Error StatusesInvalid = Error.Validation(
        "Workflow.StatusesInvalid", "حالات سير العمل يجب أن تكون موجودة ومن نفس نطاق سير العمل، والحالة المضافة مفعّلة.");

    /// <summary>R16 (part 04): a status that projects are in cannot be removed from the project workflow.</summary>
    public static Error StatusInUse(int projects) => Error.Failure(
        "Workflow.StatusInUse", $"لا يمكن إزالة الحالة من سير العمل: يوجد {projects} مشروع بهذه الحالة.");
}
