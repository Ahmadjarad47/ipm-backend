using Ipm.Domain.Common;

namespace Ipm.Domain.Git;

public static class GitErrors
{
    public static readonly Error RepositoryNotFound = Error.NotFound("Git.RepositoryNotFound", "المستودع غير موجود.");

    public static readonly Error EventNotFound = Error.NotFound("Git.EventNotFound", "حدث Git غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    public static readonly Error DeliveryNotFound = Error.NotFound("Git.DeliveryNotFound", "التسليم غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R1.</summary>
    public static readonly Error AlreadyConnected = Error.Conflict("Git.AlreadyConnected", "هذا المستودع مربوط بالمشروع مسبقاً.");

    /// <summary>R1, R14.</summary>
    public static readonly Error ProjectArchived = Error.Failure("Git.ProjectArchived", "المشروع مؤرشف ولا يمكن تعديل إعدادات Git فيه.");

    /// <summary>R2, R3, R10.</summary>
    public static readonly Error Disconnected = Error.Failure("Git.Disconnected", "المستودع مفصول؛ أعد ربطه أولاً.");

    /// <summary>R4.</summary>
    public static Error RefPatternInvalid(string reason) => Error.Failure("Git.RefPatternInvalid", reason);

    /// <summary>R5.</summary>
    public static readonly Error MissingHeaders = Error.Validation(
        "Git.MissingHeaders", "معرّف التسليم أو الطابع الزمني مفقود؛ لا يمكن ضمان عدم التكرار والحماية من إعادة الإرسال.");

    /// <summary>R5.</summary>
    public static readonly Error InvalidSignature = Error.Unauthorized("Git.InvalidSignature", "توقيع Webhook غير صالح.");

    /// <summary>R5.</summary>
    public static readonly Error StaleDelivery = Error.Unauthorized(
        "Git.StaleDelivery", "رُفض التسليم: الطابع الزمني غير صالح أو خارج نافذة 5 دقائق (حماية من إعادة الإرسال).");

    /// <summary>R6.</summary>
    public static readonly Error PayloadTooLarge = Error.Validation("Git.PayloadTooLarge", "حجم الحمولة يتجاوز 1 ميغابايت.");

    /// <summary>R9.</summary>
    public static readonly Error NotRedeliverable = Error.Failure("Git.NotRedeliverable", "إعادة المعالجة متاحة للتسليمات الفاشلة فقط.");

    /// <summary>R10.</summary>
    public static Error SyncFailed(string reason) => Error.BadGateway("Git.SyncFailed", reason);

    /// <summary>R11.</summary>
    public static readonly Error AlreadyLinked = Error.Conflict("Git.AlreadyLinked", "مرتبط مسبقاً.");

    /// <summary>R11.</summary>
    public static readonly Error LinkedItemNotFound = Error.NotFound("Git.LinkedItemNotFound", "لا يوجد عنصر بهذا الرمز في هذا المشروع.");

    /// <summary>R12.</summary>
    public static readonly Error ProjectRequired = Error.Validation("Git.ProjectRequired", "حدّد مشروعاً واحداً.");
}
