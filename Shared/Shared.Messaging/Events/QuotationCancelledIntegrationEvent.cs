namespace Shared.Messaging.Events;

/// <summary>
/// Published when an admin cancels a quotation request.
/// Triggers notifications to all invited companies and to the RM.
/// The admin's appraisal-assignment task remains open for the admin to pick a new action.
/// </summary>
public record QuotationCancelledIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid? TaskExecutionId { get; init; }
    public string? Reason { get; init; }

    /// <summary>Company IDs that were invited to this quotation.</summary>
    public Guid[] InvitedCompanyIds { get; init; } = [];

    /// <summary>AppraisalIds of all appraisals in this quotation (used by Integration to find external case key).</summary>
    public Guid[] AppraisalIds { get; init; } = [];

    /// <summary>Username of the RM who owns the linked Request.</summary>
    public string? RmUsername { get; init; }
}
