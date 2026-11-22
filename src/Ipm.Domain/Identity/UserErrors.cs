using Ipm.Domain.Common;

namespace Ipm.Domain.Identity;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("User.NotFound", "المستخدم غير موجود.");

    /// <summary>R7.</summary>
    public static readonly Error UsernameTaken = Error.Conflict("User.UsernameTaken", "اسم المستخدم مستخدم لحساب آخر.");

    /// <summary>R7.</summary>
    public static readonly Error EmailTaken = Error.Conflict("User.EmailTaken", "هذا البريد مستخدم لحساب آخر.");

    /// <summary>R9.</summary>
    public static readonly Error CannotDeactivateSelf = Error.Failure("User.CannotDeactivateSelf", "لا يمكنك تعطيل حسابك الحالي.");

    /// <summary>R10.</summary>
    public static readonly Error CannotChangeOwnRoles = Error.Forbidden("User.CannotChangeOwnRoles", "لا يمكنك تعديل أدوارك بنفسك.");

    /// <summary>R11.</summary>
    public static readonly Error TargetOutranksCaller = Error.Forbidden(
        "User.TargetOutranksCaller", "لا يمكنك تنفيذ هذا الإجراء على حساب يملك صلاحيات أعلى من صلاحياتك.");

    /// <summary>R13.</summary>
    public static readonly Error LastRoleManager = Error.Failure(
        "User.LastRoleManager", "يجب أن يبقى مستخدم نشط واحد على الأقل يملك صلاحية إدارة الأدوار.");

    /// <summary>R14.</summary>
    public static Error ManagerInvalid(string reason) => Error.Validation("User.ManagerInvalid", reason);

    /// <summary>R19.</summary>
    public static readonly Error RoleScopeInvalid = Error.Failure(
        "User.RoleScopeInvalid", "يمكن تعيين الأدوار ذات النطاق «النظام» فقط. أدوار المشروع تُسند من أعضاء المشروع.");

    public static readonly Error RoleNotFound = Error.Validation("User.RoleNotFound", "أحد الأدوار المختارة غير موجود.");
}
