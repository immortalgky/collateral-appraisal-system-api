namespace Shared.Messaging.Events;

/// <summary>
/// Published when the admin sends the shortlisted quotations to the RM for selection.
/// Triggers a notification to the RM.
/// </summary>
public record ShortlistSentToRmIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid RequestId { get; init; }

    /// <summary>UserId of the RM who owns the linked Request.</summary>
    public Guid RmUserId { get; init; }

    public Guid[] ShortlistedCompanyQuotationIds { get; init; } = [];

    /// <summary>AppraisalIds of all shortlisted appraisals in this quotation.</summary>
    public Guid[] AppraisalIds { get; init; } = [];
}
