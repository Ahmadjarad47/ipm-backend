namespace Ipm.Domain.Common;

/// <summary>Category of a business error; the API maps it to an HTTP status code.</summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    PreconditionFailed = 5,
    Unauthorized = 6,
    TooManyRequests = 7,
}

/// <summary>
/// An expected business error (not an exception). Code is stable and machine readable,
/// Description is the human readable (Arabic) message shown to the user.
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error PreconditionFailed(string code, string description) => new(code, description, ErrorType.PreconditionFailed);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error TooManyRequests(string code, string description) => new(code, description, ErrorType.TooManyRequests);

    /// <summary>The record was saved by someone else since the caller read it (stale ETag / version).</summary>
    public static readonly Error StaleVersion = PreconditionFailed(
        "Concurrency.StaleVersion",
        "تم تعديل هذا السجل من مستخدم آخر. حدّث البيانات وأعد المحاولة.");
}
