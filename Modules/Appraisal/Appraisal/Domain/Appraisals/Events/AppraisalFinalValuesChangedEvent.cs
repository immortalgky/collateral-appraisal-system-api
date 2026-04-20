namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when a PricingAnalysis FinalAppraisedValue changes,
/// triggering a recalculation of the appraisal-level summary in ValuationAnalyses.
/// </summary>
public record AppraisalFinalValuesChangedEvent(Guid PropertyGroupId) : IDomainEvent;
