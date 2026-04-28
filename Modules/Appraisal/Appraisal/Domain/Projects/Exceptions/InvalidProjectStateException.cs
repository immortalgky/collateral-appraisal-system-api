namespace Appraisal.Domain.Projects.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on a Project in an invalid state
/// or when a type-specific operation is called on a Project of the wrong ProjectType.
/// Maps to HTTP 400 via the global DomainException handler.
/// </summary>
public class InvalidProjectStateException : DomainException
{
    public InvalidProjectStateException(string message) : base(message)
    {
    }
}
