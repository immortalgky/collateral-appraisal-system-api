namespace Appraisal.Domain.Appraisals.Exceptions;

/// <summary>
/// Exception thrown when a property is not found
/// </summary>
public class PropertyNotFoundException : NotFoundException
{
    public PropertyNotFoundException(Guid id) : base($"Property with ID {id} was not found")
    {
    }
}
