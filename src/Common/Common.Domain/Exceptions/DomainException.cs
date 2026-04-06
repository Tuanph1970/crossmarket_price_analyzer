namespace Common.Domain.Exceptions;

/// <summary>
/// Base exception thrown when a domain rule is violated.
/// </summary>
public class DomainException : Exception
{
    public DomainException() { }
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
