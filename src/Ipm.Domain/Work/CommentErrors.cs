using Ipm.Domain.Common;

namespace Ipm.Domain.Work;

public static class CommentErrors
{
    public static readonly Error NotFound = Error.NotFound("Comment.NotFound", "التعليق غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R13.</summary>
    public static readonly Error SubjectNotFound = Error.NotFound("Comment.SubjectNotFound", "المشروع أو عنصر العمل غير موجود أو لا تملك صلاحية الاطلاع عليه.");

    /// <summary>R13.</summary>
    public static readonly Error ProjectArchived = Error.Failure("Comment.ProjectArchived", "المشروع مؤرشف ولا يقبل تغييرات على تعليقاته.");

    /// <summary>R14.</summary>
    public static readonly Error NotAuthor = Error.Forbidden("Comment.NotAuthor", "يمكنك تعديل تعليقاتك فقط.");

    /// <summary>R14.</summary>
    public static Error EditWindowExpired(int hours) => Error.Failure("Comment.EditWindowExpired", $"انتهت مدة تعديل التعليق ({hours} ساعة).");

    /// <summary>R14, R15.</summary>
    public static readonly Error Deleted = Error.Failure("Comment.Deleted", "التعليق محذوف.");
}
