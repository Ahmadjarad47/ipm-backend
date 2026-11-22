using Ipm.Domain.Common;

namespace Ipm.Domain.Projects;

public static class ProjectTemplateErrors
{
    public static readonly Error NotFound = Error.NotFound("ProjectTemplate.NotFound", "قالب المشروع غير موجود.");

    /// <summary>R15.</summary>
    public static readonly Error NameTaken = Error.Conflict("ProjectTemplate.NameTaken", "يوجد قالب مشروع بهذا الاسم.");

    /// <summary>R14.</summary>
    public static readonly Error InvalidTypes = Error.Failure(
        "ProjectTemplate.InvalidTypes", "أنواع القالب غير صالحة: نوع واحد على الأقل موجود، والافتراضي من المسموحة.");

    /// <summary>R14.</summary>
    public static readonly Error InvalidDuration = Error.Validation("ProjectTemplate.InvalidDuration", "المدة يجب أن تكون يوماً على الأقل.");

    /// <summary>R14.</summary>
    public static readonly Error InvalidInitialStatus = Error.Failure(
        "ProjectTemplate.InvalidInitialStatus", "الحالة الابتدائية للقالب يجب أن تكون الحالة الابتدائية لسير عمل المشاريع.");

    /// <summary>R14.</summary>
    public static readonly Error InvalidRoles = Error.Failure(
        "ProjectTemplate.InvalidRoles", "الأدوار المقترحة يجب أن تكون موجودة ونطاقها «المشروع».");

    /// <summary>R14.</summary>
    public static readonly Error InvalidMembers = Error.Failure(
        "ProjectTemplate.InvalidMembers", "الأعضاء المقترحون يجب أن يكونوا مستخدمين موجودين بأدوار نطاقها «المشروع».");
}
