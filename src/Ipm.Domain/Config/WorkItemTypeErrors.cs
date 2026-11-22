using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

public static class WorkItemTypeErrors
{
    public static readonly Error NotFound = Error.NotFound("WorkItemType.NotFound", "نوع العمل غير موجود.");

    /// <summary>R1.</summary>
    public static readonly Error KeyTaken = Error.Conflict("WorkItemType.KeyTaken", "يوجد نوع عمل بنفس المفتاح.");

    /// <summary>R1.</summary>
    public static readonly Error KeyInvalid = Error.Validation(
        "WorkItemType.KeyInvalid", "المفتاح: أحرف إنجليزية وأرقام و_ (من 2 إلى 31 خانة) ويبدأ بحرف.");

    /// <summary>R1.</summary>
    public static readonly Error KeyImmutable = Error.Failure(
        "WorkItemType.KeyImmutable", "لا يمكن تغيير مفتاح النوع بعد إنشائه. غيّر الاسم بدلاً منه.");

    /// <summary>R3. The exact problems are reported by the validator; this is the domain last line of defense.</summary>
    public static readonly Error InvalidFields = Error.Validation(
        "WorkItemType.InvalidFields", "تعريفات الحقول المخصصة غير صالحة.");

    /// <summary>R5.</summary>
    public static readonly Error ChildTypeNotFound = Error.Validation(
        "WorkItemType.ChildTypeNotFound", "أحد الأنواع الفرعية المختارة غير موجود.");

    /// <summary>R13.</summary>
    public static readonly Error WorkflowInvalid = Error.Validation(
        "WorkItemType.WorkflowInvalid", "سير العمل المختار غير موجود أو ليس سير عمل لعناصر العمل.");

    /// <summary>R14.</summary>
    public static readonly Error WorkflowRequired = Error.Validation(
        "WorkItemType.WorkflowRequired", "اختر سير عمل للنوع: لا يوجد سير العمل الافتراضي (standard).");

    /// <summary>R16 (part 04): a type that projects use as their default cannot be deactivated.</summary>
    public static Error IsProjectDefault(IEnumerable<string> projectCodes) => Error.Failure(
        "WorkItemType.IsProjectDefault", $"لا يمكن تعطيل النوع: هو النوع الافتراضي في المشاريع: {string.Join("، ", projectCodes)}.");
}
