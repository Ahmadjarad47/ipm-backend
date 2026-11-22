using Ipm.Domain.Common;

namespace Ipm.Domain.Work;

public static class WorkItemErrors
{
    /// <summary>Also the answer for an item of a project the caller cannot see, so its existence is not revealed.</summary>
    public static readonly Error NotFound = Error.NotFound("WorkItem.NotFound", "عنصر العمل غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R1, R2.</summary>
    public static readonly Error ProjectArchived = Error.Failure("WorkItem.ProjectArchived", "المشروع مؤرشف ولا يمكن تعديل عناصره.");

    /// <summary>R1.</summary>
    public static readonly Error TypeNotAllowed = Error.Failure(
        "WorkItem.TypeNotAllowed", "نوع العنصر غير موجود أو غير مفعّل أو غير مسموح به في هذا المشروع.");

    /// <summary>R1.</summary>
    public static readonly Error InvalidDates = Error.Validation("WorkItem.InvalidDates", "تاريخ الاستحقاق يسبق تاريخ البداية.");

    /// <summary>R1.</summary>
    public static readonly Error WorkflowMissing = Error.Failure("WorkItem.WorkflowMissing", "لا يوجد سير عمل صالح لهذا النوع في المشروع.");

    /// <summary>R2, R3.</summary>
    public static readonly Error Archived = Error.Failure("WorkItem.Archived", "عنصر العمل مؤرشف ولا يمكن تعديله.");

    /// <summary>R2.</summary>
    public static readonly Error StatusChangeNotAllowed = Error.Failure(
        "WorkItem.StatusChangeNotAllowed", "تُغيَّر حالة العنصر عبر الانتقالات فقط.");

    /// <summary>R2.</summary>
    public static readonly Error TypeChangeNotAllowed = Error.Failure(
        "WorkItem.TypeChangeNotAllowed", "يُغيَّر نوع العنصر عبر تغيير النوع فقط.");

    /// <summary>R2.</summary>
    public static readonly Error ParentChangeNotAllowed = Error.Failure(
        "WorkItem.ParentChangeNotAllowed", "يُغيَّر الأب عبر رابط الأب فقط.");

    /// <summary>R3.</summary>
    public static readonly Error HasActiveChildren = Error.Failure(
        "WorkItem.HasActiveChildren", "للعنصر أبناء نشطون. انقلهم أو أرشفهم أولاً.");

    /// <summary>R4.</summary>
    public static readonly Error AssigneesInvalid = Error.Validation(
        "WorkItem.AssigneesInvalid", "المسؤولون يجب أن يكونوا مستخدمين نشطين من أعضاء المشروع، بدون تكرار.");

    /// <summary>R5.</summary>
    public static readonly Error FieldsInvalid = Error.Validation("WorkItem.FieldsInvalid", "قيم الحقول المخصصة غير صالحة.");

    /// <summary>R6.</summary>
    public static readonly Error ParentDifferentProject = Error.Failure(
        "WorkItem.ParentDifferentProject", "الأب يجب أن يكون من نفس المشروع.");

    /// <summary>R6.</summary>
    public static readonly Error ParentTypeNotAllowed = Error.Failure(
        "WorkItem.ParentTypeNotAllowed", "نوع هذا العنصر غير مسموح كابن لنوع الأب.");

    /// <summary>R6.</summary>
    public static readonly Error ParentCycle = Error.Failure("WorkItem.ParentCycle", "الأب المطلوب يسبب حلقة: العنصر نفسه أو أحد أحفاده.");

    /// <summary>R6.</summary>
    public static readonly Error ParentArchived = Error.Failure("WorkItem.ParentArchived", "الأب المطلوب مؤرشف.");

    /// <summary>R6.</summary>
    public static readonly Error ParentNotFound = Error.Validation("WorkItem.ParentNotFound", "عنصر الأب غير موجود.");

    /// <summary>R7.</summary>
    public static Error TransitionNotAllowed(string from, string to) => Error.Failure(
        "WorkItem.TransitionNotAllowed", $"الانتقال من «{from}» إلى «{to}» غير معرّف في سير عمل هذا العنصر.");

    /// <summary>R7.</summary>
    public static readonly Error TargetStatusInactive = Error.Failure("WorkItem.TargetStatusInactive", "الحالة المطلوبة معطّلة.");

    /// <summary>R7.</summary>
    public static readonly Error AssigneeRequired = Error.Failure(
        "WorkItem.AssigneeRequired", "هذا الانتقال يتطلب أن يكون للعنصر مسؤول.");

    /// <summary>R7.</summary>
    public static readonly Error BlockedByDependencies = Error.Failure(
        "WorkItem.BlockedByDependencies", "العنصر محجوب باعتماديات مفتوحة.");

    /// <summary>R8.</summary>
    public static readonly Error ApprovalRequired = Error.Failure(
        "WorkItem.ApprovalRequired", "هذا الانتقال يتطلب طلب موافقة معتمداً. محرك الموافقات لم يُفعَّل بعد، ويتنفّذ فقط بصلاحية تجاوز القيود المحمية.");

    /// <summary>R9.</summary>
    public static readonly Error CloseTargetRequired = Error.Validation(
        "WorkItem.CloseTargetRequired", "للعنصر أكثر من حالة إغلاق ممكنة. حدّد الحالة المطلوبة.");

    /// <summary>R9.</summary>
    public static readonly Error CloseTargetInvalid = Error.Failure(
        "WorkItem.CloseTargetInvalid", "لا يوجد انتقال إغلاق صالح من الحالة الحالية، أو الحالة المطلوبة ليست حالة إنجاز.");

    /// <summary>R9.</summary>
    public static readonly Error ReopenReasonRequired = Error.Validation(
        "WorkItem.ReopenReasonRequired", "سبب إعادة الفتح مطلوب.");

    /// <summary>R9.</summary>
    public static readonly Error NotClosed = Error.Failure("WorkItem.NotClosed", "العنصر غير مغلق، فلا يمكن إعادة فتحه.");

    /// <summary>R9.</summary>
    public static readonly Error ReopenTargetInvalid = Error.Failure(
        "WorkItem.ReopenTargetInvalid", "لا يوجد انتقال إعادة فتح صالح من الحالة الحالية، أو الحالة المطلوبة مغلقة.");

    /// <summary>R10.</summary>
    public static readonly Error TypeChangeConflictsError = Error.Failure(
        "WorkItem.TypeChangeConflicts", "تغيير النوع يتعارض مع بيانات العنصر. راجع قائمة التعارضات.");

    /// <summary>R12.</summary>
    public static readonly Error BulkEmpty = Error.Validation("Bulk.Empty", "حدّد عنصراً واحداً على الأقل.");

    /// <summary>R12.</summary>
    public static readonly Error BulkTooMany = Error.Validation("Bulk.TooMany", "الحد الأقصى 100 عنصر في العملية الواحدة.");

    /// <summary>R12.</summary>
    public static readonly Error BulkInvalid = Error.Validation("Bulk.Invalid", "إجراء التعديل الجماعي غير صالح.");
}

/// <summary>One reason a type change cannot happen (R10): which part of the item and why.</summary>
public sealed record TypeChangeConflict(string Field, string Reason);
