namespace Common.Domain.Exceptions;

/// <summary>
/// Base exception thrown from the application layer (CQRS handlers).
/// </summary>
public class ApplicationException : Exception
{
    public string[]? Errors { get; }

    public ApplicationException() { }
    public ApplicationException(string message) : base(message) { }
    public ApplicationException(string message, Exception innerException)
        : base(message, innerException) { }
    public ApplicationException(string message, string[] errors)
        : base(message) => Errors = errors;
}
