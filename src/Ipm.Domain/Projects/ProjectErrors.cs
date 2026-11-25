using Ipm.Domain.Common;

namespace Ipm.Domain.Projects;

public static class ProjectErrors
{
    /// <summary>Also the answer for a project the caller cannot see, so its existence is not revealed.</summary>
    public static readonly Error NotFound = Error.NotFound("Project.NotFound", "المشروع غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R1.</summary>
    public static readonly Error CodeInvalid = Error.Validation(
        "Project.CodeInvalid", "رمز المشروع يبدأ بحرف لاتيني ويتكون من 2 إلى 10 أحرف لاتينية كبيرة أو أرقام.");

    /// <summary>R1.</summary>
    public static readonly Error CodeTaken = Error.Conflict("Project.CodeTaken", "رمز المشروع مستخدم لمشروع آخر.");

    /// <summary>R1.</summary>
    public static readonly Error CodeImmutable = Error.Failure(
        "Project.CodeImmutable", "لا يمكن تغيير رمز المشروع بعد إنشائه.");

    /// <summary>R2.</summary>
    public static readonly Error InvalidDates = Error.Validation("Project.InvalidDates", "تاريخ النهاية يسبق تاريخ البداية.");

    /// <summary>R3.</summary>
    public static readonly Error OwnerInvalid = Error.Validation(
        "Project.OwnerInvalid", "المالك يجب أن يكون مستخدماً نشطاً موجوداً.");

    /// <summary>R3.</summary>
    public static readonly Error OwnerMustBeMember = Error.Failure(
        "Project.OwnerMustBeMember", "مالك المشروع يجب أن يكون من أعضائه. أضفه إلى الأعضاء أولاً.");

    /// <summary>R4.</summary>
    public static readonly Error WorkflowMissing = Error.Failure(
        "Project.WorkflowMissing", "لا يوجد سير عمل للمشاريع بالمفتاح project_default.");

    /// <summary>R4.</summary>
    public static readonly Error ManagerRoleMissing = Error.Failure(
        "Project.ManagerRoleMissing", "لا يوجد دور «مدير مشروع» (project_manager) لإضافة المالك به.");

    /// <summary>R5.</summary>
    public static readonly Error TemplateInactive = Error.Failure("Project.TemplateInactive", "قالب المشروع معطّل.");

    /// <summary>R5.</summary>
    public static readonly Error TemplateNotFound = Error.Validation("Project.TemplateNotFound", "قالب المشروع غير موجود.");

    /// <summary>R6.</summary>
    public static readonly Error Archived = Error.Failure("Project.Archived", "المشروع مؤرشف ولا يمكن تعديله.");

    /// <summary>R7.</summary>
    public static Error MembersInvalid(string reason) => Error.Failure("Project.MembersInvalid", reason);

    /// <summary>R9.</summary>
    public static readonly Error StatusChangeNotAllowed = Error.Failure(
        "Project.StatusChangeNotAllowed", "تُغيَّر حالة المشروع عبر انتقالات سير العمل فقط.");

    /// <summary>R10.</summary>
    public static Error TransitionNotAllowed(string from, string to) => Error.Failure(
        "Project.TransitionNotAllowed", $"الانتقال من «{from}» إلى «{to}» غير معرّف في سير عمل المشاريع.");

    /// <summary>R10.</summary>
    public static readonly Error TargetStatusInactive = Error.Failure(
        "Project.TargetStatusInactive", "الحالة المطلوبة معطّلة.");

    /// <summary>R11.</summary>
    public static readonly Error ApprovalRequired = Error.Failure(
        "Project.ApprovalRequired", "هذا الانتقال يتطلب طلب موافقة معتمداً. محرك الموافقات لم يُفعَّل بعد، ويتنفّذ فقط بصلاحية تجاوز القيود المحمية.");

    /// <summary>R12.</summary>
    public static readonly Error TypesInvalid = Error.Failure(
        "Project.TypesInvalid", "يجب السماح بنوع واحد على الأقل موجود، والنوع الافتراضي من المسموحة ومفعّل.");

    /// <summary>R17 (part 04), enforced from part 05: narrowing the allowed types under existing items of a removed type.</summary>
    public static Error TypesInUse(int items) => Error.Failure(
        "Project.TypesInUse", $"لا يمكن حذف نوع مسموح به: يوجد {items} عمل من أنواع محذوفة في هذا المشروع.");

    /// <summary>R17 (part 04), enforced from part 05: an override whose workflow lacks the status of existing items.</summary>
    public static Error WorkflowOverrideConflicts(int items) => Error.Failure(
        "Project.WorkflowOverrideConflicts", $"لا يمكن تطبيق استثناء سير العمل: يوجد {items} عمل بحالة غير موجودة في سير العمل الجديد.");

    /// <summary>R13.</summary>
    public static readonly Error WorkflowOverrideInvalid = Error.Failure(
        "Project.WorkflowOverrideInvalid", "استثناء سير العمل غير صالح: النوع أو سير العمل غير موجود، أو سير العمل ليس للأعمال.");
}
