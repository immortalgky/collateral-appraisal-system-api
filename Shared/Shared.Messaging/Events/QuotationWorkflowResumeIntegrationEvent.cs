namespace Shared.Messaging.Events;

/// <summary>
/// Signals the Workflow module to resume a step in the quotation child workflow.
/// Published by every quotation command handler that drives a workflow transition.
///
/// The Workflow module consumer resolves the child workflow instance via CorrelationId = QuotationRequestId
/// and calls ResumeWorkflowAsync with the supplied ActivityId, DecisionTaken, and optional extra data.
/// </summary>
public record QuotationWorkflowResumeIntegrationEvent : IntegrationEvent
{
    /// <summary>Correlation ID of the quotation child workflow instance.</summary>
    public Guid QuotationRequestId { get; init; }

    /// <summary>The activity that should be resumed (e.g. "ext-collect-submissions").</summary>
    public string ActivityId { get; init; } = string.Empty;

    /// <summary>The decision the actor took (e.g. "Submit", "SendToRm", "Pick").</summary>
    public string DecisionTaken { get; init; } = string.Empty;

    /// <summary>UserId of the actor completing the step.</summary>
    public string CompletedBy { get; init; } = string.Empty;

    /// <summary>For fan-out steps: the company whose task is being resolved.</summary>
    public Guid? CompanyId { get; init; }

    /// <summary>For rm-pick-winner: the selected tentative winner company quotation ID.</summary>
    public Guid? TentativeWinnerCompanyQuotationId { get; init; }

    /// <summary>For rm-pick-winner: the CompanyId of the tentative winner (needed to scope ext-respond-negotiation).</summary>
    public Guid? TentativeWinnerCompanyId { get; init; }

    /// <summary>For rm-pick-winner: whether the RM requests a negotiation round.</summary>
    public bool RmRequestsNegotiation { get; init; }

    /// <summary>For rm-pick-winner: optional negotiation note from the RM.</summary>
    public string? RmNegotiationNote { get; init; }
}
