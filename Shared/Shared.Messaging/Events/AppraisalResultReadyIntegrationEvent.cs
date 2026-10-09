namespace Shared.Messaging.Events;

/// <summary>
/// Raised once the appraisal's result package is settled — i.e. the post-approval Appraisal Summary
/// has been generated and attached, or generation has failed permanently.
///
/// This is the trigger for the outbound APPRAISAL_COMPLETED webhook. It deliberately replaces
/// <see cref="AppraisalCompletedIntegrationEvent"/> for that one consumer so LOS is never told the
/// case is done while the summary PDF is still rendering — pulling GET /api/v2/appraisals/{no}/result
/// in that window returns the pre-approval document (or none at all).
///
/// It is published exactly once per generation attempt in BOTH outcomes. A failed render must still
/// publish (with <see cref="DocumentReady"/> = false) — gating the webhook on success would leave the
/// case silently stalled on the LOS side, which is worse than delivering an incomplete package.
/// </summary>
public record AppraisalResultReadyIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }

    public Guid RequestId { get; init; }

    /// <summary>
    /// When the committee approved the appraisal (appraisal.CompletedAt), carried for consumers that need
    /// the decision date. NOT the webhook's occurredAt — that uses <see cref="IntegrationEvent.OccurredOn"/>,
    /// stamped when this event is published, so an admin regenerate months later is not dated back to the
    /// original decision.
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// false = summary generation failed permanently. The webhook still fires so the case never
    /// stalls, but the result package is incomplete until an admin re-runs the generation.
    /// </summary>
    public bool DocumentReady { get; init; }

    /// <summary>Populated only when <see cref="DocumentReady"/> is false.</summary>
    public string? FailureReason { get; init; }
}
