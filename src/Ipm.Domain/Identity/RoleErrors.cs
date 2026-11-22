using Ipm.Domain.Common;

namespace Ipm.Domain.Identity;

public static class RoleErrors
{
    public static readonly Error NotFound = Error.NotFound("Role.NotFound", "الدور غير موجود.");

    /// <summary>R17.</summary>
    public static readonly Error NameTaken = Error.Conflict("Role.NameTaken", "يوجد دور بهذا الاسم.");

    /// <summary>R12.</summary>
    public static Error PermissionNotGrantable(IEnumerable<string> keys) => Error.Forbidden(
        "Role.PermissionNotGrantable", $"لا تملك حق تفويض الصلاحيات: {string.Join("، ", keys)}.");

    /// <summary>R15.</summary>
    public static readonly Error BuiltInCannotBeDeleted = Error.Failure(
        "Role.BuiltInCannotBeDeleted", "الأدوار المدمجة في النظام لا تُحذف.");

    /// <summary>R15.</summary>
    public static readonly Error ImmutableField = Error.Failure(
        "Role.ImmutableField", "لا يمكن تغيير نطاق الدور بعد إنشائه.");

    /// <summary>R16.</summary>
    public static Error InUse(int count) => Error.Conflict(
        "Role.InUse", $"لا يمكن حذف الدور: يوجد {count} مستخدم مرتبط به. غيّر أدوارهم أولاً.");

    /// <summary>R18.</summary>
    public static Error UnknownPermissions(IEnumerable<string> keys) => Error.Validation(
        "Role.UnknownPermissions", $"صلاحيات غير معروفة: {string.Join("، ", keys)}");

    /// <summary>R18.</summary>
    public static readonly Error PermissionConflict = Error.Validation(
        "Role.PermissionConflict", "لا يمكن منح صلاحية وحظرها في نفس الدور.");
}
