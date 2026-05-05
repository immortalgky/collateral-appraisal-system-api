namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when a ProjectModel-level PricingAnalysis FinalAppraisedValue changes.
/// Future subscribers can use this to sync the model's standard price to downstream systems.
/// </summary>
public record ProjectModelPricingFinalValueChangedEvent(
    Guid PricingAnalysisId,
    Guid ProjectModelId,
    decimal? FinalAppraisedValue) : IDomainEvent;
