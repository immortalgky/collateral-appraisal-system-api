namespace Shared.Messaging.Events;

/// <summary>
/// Published when the admin sends the shortlisted quotations to the RM for selection.
/// Triggers a notification to the RM.
/// </summary>
public record ShortlistSentToRmIntegrationEvent : IntegrationEvent
{
    public Guid QuotationRequestId { get; init; }
    public Guid RequestId { get; init; }

    /// <summary>Username/staff code of the RM (e.g. "P5229") for CLS to echo back on selection.</summary>
    public string? RmUsername { get; init; }

    public Guid[] ShortlistedCompanyQuotationIds { get; init; } = [];

    /// <summary>AppraisalIds of all shortlisted appraisals in this quotation.</summary>
    public Guid[] AppraisalIds { get; init; } = [];
}
