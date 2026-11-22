using Ipm.Domain.Common;

namespace Ipm.Domain.Identity;

public static class AuthErrors
{
    public static readonly Error Unauthenticated = Error.Unauthorized("Auth.Unauthenticated", "يجب تسجيل الدخول.");

    /// <summary>R1. Same message for unknown, wrong password and disabled, so it reveals nothing.</summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials", "اسم المستخدم أو كلمة المرور غير صحيحة.");

    /// <summary>R2.</summary>
    public static readonly Error LockedOut = Error.TooManyRequests(
        "Auth.LockedOut", "تم قفل الدخول مؤقتاً بسبب محاولات فاشلة متكررة. أعد المحاولة بعد 15 دقيقة.");

    /// <summary>R4.</summary>
    public static readonly Error RefreshInvalid = Error.Unauthorized(
        "Auth.RefreshInvalid", "انتهت الجلسة. سجّل الدخول من جديد.");

    /// <summary>R6.</summary>
    public static readonly Error PasswordChangeRequired = Error.Forbidden(
        "Auth.PasswordChangeRequired", "يجب تغيير كلمة المرور المؤقتة قبل متابعة استخدام النظام.");

    public static readonly Error WrongCurrentPassword = Error.Failure(
        "Auth.WrongCurrentPassword", "كلمة المرور الحالية غير صحيحة.");

    /// <summary>R5.</summary>
    public static Error WeakPassword(string details) => Error.Validation("Auth.WeakPassword", details);
}
