using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

public static class StatusErrors
{
    public static readonly Error NotFound = Error.NotFound("Status.NotFound", "الحالة غير موجودة.");

    /// <summary>R1.</summary>
    public static readonly Error KeyTaken = Error.Conflict("Status.KeyTaken", "يوجد حالة بنفس المفتاح في هذا النطاق.");

    /// <summary>R1.</summary>
    public static readonly Error KeyInvalid = Error.Validation(
        "Status.KeyInvalid", "المفتاح: أحرف إنجليزية وأرقام و_ (من 2 إلى 31 خانة) ويبدأ بحرف.");

    /// <summary>R1.</summary>
    public static readonly Error KeyImmutable = Error.Failure(
        "Status.KeyImmutable", "لا يمكن تغيير مفتاح الحالة بعد إنشائها. غيّر الاسم بدلاً منه.");

    /// <summary>R2.</summary>
    public static readonly Error ScopeImmutable = Error.Failure(
        "Status.ScopeImmutable", "لا يمكن تغيير نطاق الحالة بعد إنشائها.");

    /// <summary>R11.</summary>
    public static Error IsInitialStatus(IEnumerable<string> workflowNames) => Error.Failure(
        "Status.IsInitialStatus", $"لا يمكن تعطيل الحالة: هي الحالة الابتدائية لسير العمل: {string.Join("، ", workflowNames)}.");

    /// <summary>R16 (part 04): a project status that projects are in cannot be deactivated.</summary>
    public static Error InUse(int projects) => Error.Failure(
        "Status.InUse", $"لا يمكن تعطيل الحالة: يوجد {projects} مشروع بهذه الحالة.");
}
