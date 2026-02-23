namespace Appraisal.Domain.Appraisals.Exceptions;

/// <summary>
/// Exception thrown when an appraisal is not found
/// </summary>
public class AppraisalNotFoundException : NotFoundException
{
    public AppraisalNotFoundException(Guid id) : base($"Appraisal with ID {id} was not found")
    {
    }

    public AppraisalNotFoundException(string appraisalNumber) : base($"Appraisal '{appraisalNumber}' was not found")
    {
    }
}