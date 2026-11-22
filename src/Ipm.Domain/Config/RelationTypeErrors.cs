using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

public static class RelationTypeErrors
{
    public static readonly Error NotFound = Error.NotFound("RelationType.NotFound", "نوع الرابط غير موجود.");

    /// <summary>R6.</summary>
    public static readonly Error KeyTaken = Error.Conflict("RelationType.KeyTaken", "يوجد نوع رابط بنفس المفتاح.");

    /// <summary>R6.</summary>
    public static readonly Error KeyInvalid = Error.Validation(
        "RelationType.KeyInvalid", "المفتاح: أحرف إنجليزية وأرقام و_ (من 2 إلى 31 خانة) ويبدأ بحرف.");

    /// <summary>R6.</summary>
    public static readonly Error KeyImmutable = Error.Failure(
        "RelationType.KeyImmutable", "لا يمكن تغيير مفتاح نوع الرابط بعد إنشائه. غيّر الاسم بدلاً منه.");

    /// <summary>R7.</summary>
    public static readonly Error InverseNameMustMatch = Error.Validation(
        "RelationType.InverseNameMustMatch", "الرابط غير الموجّه يجب أن يكون اسمه العكسي مطابقاً لاسمه.");

    /// <summary>R7.</summary>
    public static readonly Error AcyclicNeedsDirected = Error.Validation(
        "RelationType.AcyclicNeedsDirected", "منع الحلقات يخص الروابط الموجّهة فقط.");

    /// <summary>R9 (part 02), enforced from part 05b: once links use the type, its direction and cycle rule are frozen and pairs cannot be narrowed under them.</summary>
    public static Error LockedAfterUse(string reason) => Error.Failure("RelationType.LockedAfterUse", reason);

    /// <summary>R8.</summary>
    public static readonly Error PairTypeNotFound = Error.Validation(
        "RelationType.PairTypeNotFound", "أزواج الأنواع المسموحة تتضمن نوع عمل غير موجود.");
}
