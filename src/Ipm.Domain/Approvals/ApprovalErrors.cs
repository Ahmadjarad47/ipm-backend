using Ipm.Domain.Common;

namespace Ipm.Domain.Approvals;

public static class ApprovalErrors
{
    /// <summary>Also the answer for a request the caller has no part in, so its existence is not revealed.</summary>
    public static readonly Error NotFound = Error.NotFound("Approval.NotFound", "طلب الموافقة غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    public static readonly Error TemplateNotFound = Error.NotFound("ApprovalTemplate.NotFound", "قالب الموافقة غير موجود.");

    public static readonly Error SubjectNotFound = Error.NotFound("Approval.SubjectNotFound", "المشروع أو عنصر العمل غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R4.</summary>
    public static readonly Error TemplateInactive = Error.Failure("Approval.TemplateInactive", "قالب الموافقة معطّل.");

    /// <summary>R4.</summary>
    public static readonly Error TemplateNotApplicable = Error.Failure("Approval.TemplateNotApplicable", "قالب الموافقة لا ينطبق على هذا العنصر وفق قواعده.");

    /// <summary>R4.</summary>
    public static readonly Error ActionMismatch = Error.Failure("Approval.ActionMismatch", "الإجراء لا يتوافق مع قالب الموافقة.");

    /// <summary>R4: sensitive changes arrive with part 06b.</summary>
    public static readonly Error SensitiveNotSupported = Error.Failure(
        "Approval.SensitiveNotSupported", "طلبات التعديل الحساس غير متاحة بعد.");

    /// <summary>R4.</summary>
    public static readonly Error ProjectArchived = Error.Failure("Approval.ProjectArchived", "المشروع مؤرشف ولا يقبل طلبات موافقة.");

    /// <summary>R4.</summary>
    public static readonly Error AlreadyPending = Error.Conflict("Approval.AlreadyPending", "يوجد طلب موافقة معلّق لهذا العنصر.");

    /// <summary>R4.</summary>
    public static Error StageUnresolvable(int order, string name) => Error.Failure(
        "Approval.StageUnresolvable", $"تعذّر تحديد معتمد للمرحلة {order} «{name}» لهذا الطلب.");

    /// <summary>R6.</summary>
    public static readonly Error Stale = Error.Conflict("Approval.Stale", "هذا الإصدار من الطلب أُبطل؛ لا يُقبل قرار متأخر عليه.");

    /// <summary>R6.</summary>
    public static readonly Error NotPending = Error.Failure("Approval.NotPending", "الطلب ليس بانتظار قرار.");

    /// <summary>R6.</summary>
    public static readonly Error SelfDecision = Error.Forbidden("Approval.SelfDecision", "لا يمكنك اعتماد طلب قدّمته بنفسك.");

    /// <summary>R6.</summary>
    public static readonly Error NotApprover = Error.Forbidden("Approval.NotApprover", "ليست لك صلاحية القرار في المرحلة الحالية.");

    /// <summary>R6.</summary>
    public static readonly Error CommentRequired = Error.Validation("Approval.CommentRequired", "التعليق إلزامي عند الرفض أو الإرجاع.");

    /// <summary>R7.</summary>
    public static readonly Error NotResubmittable = Error.Failure("Approval.NotResubmittable", "لا يمكن إعادة تقديم هذا الطلب.");

    /// <summary>R7.</summary>
    public static readonly Error NotRequester = Error.Forbidden("Approval.NotRequester", "إعادة التقديم متاحة لصاحب الطلب فقط.");

    /// <summary>R8.</summary>
    public static readonly Error BulkInvalid = Error.Validation("Approval.BulkInvalid", "اختر من 1 إلى 50 طلباً، وحدّد رقم الإصدار الذي راجعته لكل طلب.");

    /// <summary>R9.</summary>
    public static readonly Error ProtectedFieldsChanged = Error.Failure(
        "Approval.ProtectedFieldsChanged", "تغيّرت بيانات محمية بعد الموافقة؛ أُبطلت الموافقة ويلزم طلب جديد.");

    /// <summary>R14.</summary>
    public static readonly Error RemindForbidden = Error.Forbidden("Approval.RemindForbidden", "التذكير متاح لمقدّم الطلب أو مدير الموافقات.");

    /// <summary>R14.</summary>
    public static readonly Error RemindTooSoon = Error.TooManyRequests("Approval.RemindTooSoon", "أُرسل تذكير خلال الساعة الماضية؛ حاول لاحقاً.");

    /// <summary>R2 of the template: a template with requests cannot change the event it governs.</summary>
    public static readonly Error EventLocked = Error.Failure("ApprovalTemplate.EventLocked", "لا يمكن تغيير حدث قالب تقدّمت منه طلبات.");

    public static readonly Error TemplateInvalid = Error.Validation("ApprovalTemplate.Invalid", "قالب الموافقة غير صالح.");

    public static readonly Error DelegationNotFound = Error.NotFound("Delegation.NotFound", "التفويض غير موجود.");

    /// <summary>R13.</summary>
    public static readonly Error DelegationOverlap = Error.Conflict("Delegation.Overlap", "يوجد تفويض فعّال متداخل لنفس المستخدم.");

    /// <summary>R13.</summary>
    public static readonly Error DelegationRevoked = Error.Failure("Delegation.AlreadyRevoked", "التفويض ملغى مسبقاً.");

    /// <summary>R13.</summary>
    public static readonly Error DelegatorInvalid = Error.Failure("Delegation.DelegatorInvalid", "المفوِّض غير موجود أو غير نشط.");
}
