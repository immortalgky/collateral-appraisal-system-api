namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Appraisal module whenever the appraisal-level appraised value is
/// (re)computed — i.e. whenever a PricingAnalysis FinalAppraisedValue changes and the
/// ValuationAnalyses summary is upserted.
///
/// Consumed by the Workflow module to write <c>appraisalValue</c> into
/// <c>WorkflowInstance.Variables</c> so the approval-tier SwitchActivity and the
/// committee-selection ApprovalActivity route on appraised value rather than facility limit.
/// </summary>
public class AppraisalValueChangedIntegrationEvent
{
    /// <summary>Appraisal whose appraised value changed.</summary>
    public Guid AppraisalId { get; init; }

    /// <summary>
    /// Workflow correlation ID (= Appraisal.RequestId = Request.Id). Used to find the
    /// WorkflowInstance that must receive the updated variable.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Latest appraisal-level appraised value (sum of each group's FinalAppraisedValue).</summary>
    public decimal AppraisedValue { get; init; }

    public DateTime OccurredOn { get; init; }
}
