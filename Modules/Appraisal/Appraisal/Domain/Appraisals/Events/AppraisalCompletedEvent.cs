namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when an appraisal is completed
/// </summary>
public record AppraisalCompletedEvent(Appraisal Appraisal) : IDomainEvent;