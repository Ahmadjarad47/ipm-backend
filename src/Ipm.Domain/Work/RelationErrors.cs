using Ipm.Domain.Common;

namespace Ipm.Domain.Work;

public static class RelationErrors
{
    public static readonly Error NotFound = Error.NotFound("Relation.NotFound", "الرابط غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R1.</summary>
    public static readonly Error TypeNotFound = Error.Validation("Relation.TypeNotFound", "نوع الرابط غير موجود.");

    /// <summary>R1.</summary>
    public static readonly Error TypeInactive = Error.Failure("Relation.TypeInactive", "نوع الرابط معطّل.");

    /// <summary>R1.</summary>
    public static readonly Error SelfLink = Error.Failure("Relation.SelfLink", "لا يمكن ربط العمل بنفسه.");

    /// <summary>R1.</summary>
    public static readonly Error ItemArchived = Error.Failure("Relation.ItemArchived", "أحد العملين مؤرشف ولا يقبل روابط جديدة.");

    /// <summary>R2, R7.</summary>
    public static readonly Error ProjectArchived = Error.Failure("Relation.ProjectArchived", "المشروع مؤرشف ولا تُعدَّل روابط أعماله.");

    /// <summary>R2.</summary>
    public static readonly Error CrossProjectNotAllowed = Error.Failure(
        "Relation.CrossProjectNotAllowed", "الروابط بين المشاريع غير مسموحة حالياً.");

    /// <summary>R3.</summary>
    public static Error PairNotAllowed(string type, string source, string target) => Error.Failure(
        "Relation.PairNotAllowed", $"نوع الرابط «{type}» لا يسمح بالربط بين «{source}» و«{target}».");

    /// <summary>R4.</summary>
    public static Error Duplicate(bool directed) => Error.Conflict(
        "Relation.Duplicate", directed ? "هذا الرابط أو عكسه موجود مسبقاً." : "هذا الرابط موجود مسبقاً.");

    /// <summary>R5.</summary>
    public static Error Cycle(string type) => Error.Failure("Relation.Cycle", $"الرابط يُنشئ حلقة دائرية من نوع «{type}».");
}
