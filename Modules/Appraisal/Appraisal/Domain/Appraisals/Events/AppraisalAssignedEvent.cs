namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when an appraisal is assigned
/// </summary>
public record AppraisalAssignedEvent(Appraisal Appraisal, AppraisalAssignment Assignment) : IDomainEvent;