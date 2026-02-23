namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when an appraisal status changes
/// </summary>
public record AppraisalStatusChangedEvent(
    Appraisal Appraisal,
    AppraisalStatus OldStatus,
    AppraisalStatus NewStatus) : IDomainEvent;