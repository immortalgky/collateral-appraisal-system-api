namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when a new appraisal is created
/// </summary>
public record AppraisalCreatedEvent(Appraisal Appraisal) : IDomainEvent;