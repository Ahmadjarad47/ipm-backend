namespace Ipm.Application.Common.Exceptions;

/// <summary>Thrown by <c>ValidationBehavior</c> when a command or query fails its FluentValidation rules.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") => Errors = errors;

    public ValidationException() : this(new Dictionary<string, string[]>()) { }

    public ValidationException(string message) : base(message) => Errors = new Dictionary<string, string[]>();

    public ValidationException(string message, Exception innerException) : base(message, innerException) =>
        Errors = new Dictionary<string, string[]>();

    /// <summary>Property name to its error messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
