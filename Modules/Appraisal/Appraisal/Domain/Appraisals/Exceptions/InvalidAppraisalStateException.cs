namespace Appraisal.Domain.Appraisals.Exceptions;

/// <summary>
/// Exception thrown when an operation is attempted on an appraisal in an invalid state
/// </summary>
public class InvalidAppraisalStateException : DomainException
{
    public InvalidAppraisalStateException(string message) : base(message)
    {
    }
}