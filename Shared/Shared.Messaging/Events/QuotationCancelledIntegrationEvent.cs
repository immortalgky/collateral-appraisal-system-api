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

    /// <summary>UserId of the RM who owns the linked Request.</summary>
    public Guid? RmUserId { get; init; }
}
