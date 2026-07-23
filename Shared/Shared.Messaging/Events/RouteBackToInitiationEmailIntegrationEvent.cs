namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Workflow module (via outbox in TaskCompletedDomainEventHandler) when IntAdmin
/// routes an appraisal back to the appraisal-initiation activity to fix collateral data
/// ("Request More Info": ActivityId "appraisal-assignment", Movement "B", ActionTaken "R").
/// Consumed by the Notification module, which resolves the RM (recipient) + the acting user's
/// display name via <c>IUserLookupService</c>, renders the email, and delivers it via SMTP.
/// </summary>
public record RouteBackToInitiationEmailIntegrationEvent : IntegrationEvent
{
    /// <summary>The appraisal being routed back — used as the send-log ReferenceId.</summary>
    public Guid AppraisalId { get; init; }

    /// <summary>RM bank code (request maker / workflow StartedBy). The consumer resolves email + display name.</summary>
    public string? RmUsername { get; init; }

    /// <summary>Bank code of the user who made the route-back decision. The consumer resolves the signature name.</summary>
    public string? ActingUsername { get; init; }

    public string? CustomerName { get; init; }
    public string? AppraisalNumber { get; init; }

    /// <summary>The decision remark ([รายละเอียดNotification]) — what to correct.</summary>
    public string? Remark { get; init; }

    /// <summary>Resolved route-back reason description (from the RoutebackReason parameter group) — used as the subject.</summary>
    public string? ReasonText { get; init; }
}
