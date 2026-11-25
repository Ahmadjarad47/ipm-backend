using Ipm.Domain.Common;

namespace Ipm.Domain.Approvals;

public static class ApprovalErrors
{
    /// <summary>Also the answer for a request the caller has no part in, so its existence is not revealed.</summary>
    public static readonly Error NotFound = Error.NotFound("Approval.NotFound", "طلب الموافقة غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    public static readonly Error TemplateNotFound = Error.NotFound("ApprovalTemplate.NotFound", "قالب الموافقة غير موجود.");

    public static readonly Error SubjectNotFound = Error.NotFound("Approval.SubjectNotFound", "المشروع أو العمل غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R4.</summary>
    public static readonly Error TemplateInactive = Error.Failure("Approval.TemplateInactive", "قالب الموافقة معطّل.");

    /// <summary>R4.</summary>
    public static readonly Error TemplateNotApplicable = Error.Failure("Approval.TemplateNotApplicable", "قالب الموافقة لا ينطبق على هذا العمل وفق قواعده.");

    /// <summary>R4.</summary>
    public static readonly Error ActionMismatch = Error.Failure("Approval.ActionMismatch", "الإجراء لا يتوافق مع قالب الموافقة.");

    /// <summary>R4: sensitive changes arrive with part 06b.</summary>

    /// <summary>R4.</summary>
    public static readonly Error ProjectArchived = Error.Failure("Approval.ProjectArchived", "المشروع مؤرشف ولا يقبل طلبات موافقة.");

    /// <summary>R4: an approved request that was not used yet already covers this action.</summary>
    public static readonly Error AlreadyApproved = Error.Conflict("Approval.AlreadyApproved", "يوجد طلب معتمد لهذا الإجراء لم يُستخدم بعد؛ نفّذ الانتقال به بدل طلب جديد.");

    /// <summary>R4: a project is closed only while it is running.</summary>
    public static readonly Error ProjectNotRunning = Error.Failure("Approval.ProjectNotRunning", "إغلاق المشروع متاح فقط أثناء تنفيذه.");

    /// <summary>R4: a project is approved once to start, and again only to reopen it after it was closed.</summary>
    public static readonly Error ProjectAlreadyRunning = Error.Failure("Approval.ProjectAlreadyRunning", "المشروع قيد التنفيذ؛ لا يحتاج اعتماد بدء. المتاح الآن هو طلب الإغلاق.");

    /// <summary>R4.</summary>
    public static readonly Error AlreadyPending = Error.Conflict("Approval.AlreadyPending", "يوجد طلب موافقة معلّق لهذا العمل.");

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

    /// <summary>06b R1: the subject (a work item) is archived.</summary>
    public static readonly Error SubjectArchived = Error.Failure("Approval.SubjectArchived", "العمل مؤرشف ولا يقبل طلبات تعديل.");

    /// <summary>06b R1: a sensitive change names no values.</summary>
    public static readonly Error ProposedChangesRequired = Error.Failure("Approval.ProposedChangesRequired", "حدّد القيم المقترحة للحقول المحمية.");

    /// <summary>06b R1: only a sensitive-change template takes proposed values.</summary>
    public static readonly Error ProposedChangesNotAllowed = Error.Failure(
        "Approval.ProposedChangesNotAllowed", "القيم المقترحة متاحة لقوالب التعديل الحساس فقط.");

    /// <summary>06b R2.</summary>
    public static Error FieldsNotProtected(IEnumerable<string> fields) => Error.Failure(
        "Approval.FieldsNotProtected", $"حقول غير محمية بهذا القالب أو غير قابلة للاقتراح: {string.Join("، ", fields)}.");

    /// <summary>06b R5.</summary>
    public static readonly Error NoChange = Error.Failure("Approval.NoChange", "القيم المقترحة مطابقة للحالية؛ لا يوجد تعديل لطلبه.");

    /// <summary>06b R6: the protected values moved after the request was submitted, so what the approvers saw is stale.</summary>
    public static readonly Error ProtectedFieldsDrifted = Error.Conflict(
        "Approval.ProtectedFieldsChanged", "تغيّرت بيانات محمية بعد تقديم الطلب؛ أعد تقديمه ليُراجَع من جديد.");

    /// <summary>06b R8: the field is protected by an active sensitive-change template that governs the subject.</summary>
    public static Error ChangeRequiresApproval(string template, IEnumerable<string> fields) => Error.Failure(
        "Approval.ChangeRequiresApproval", $"تعديل {string.Join("، ", fields)} يتطلب طلب موافقة «{template}» ولا يُنفَّذ مباشرة.");
}
