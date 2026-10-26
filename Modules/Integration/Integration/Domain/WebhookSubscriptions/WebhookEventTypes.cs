namespace Integration.Domain.WebhookSubscriptions;

/// <summary>
/// The event types the outbound webhook consumers send. The admin UI lists them from <see cref="All"/>
/// when picking a subscription's EventType (null is a catch-all).
/// </summary>
public static class WebhookEventTypes
{
    public const string AppraisalCancelled = "APPRAISAL_CANCELLED";
    public const string AppraisalCompleted = "APPRAISAL_COMPLETED";
    public const string AppraisalCreated = "APPRAISAL_CREATED";
    public const string AppraisalPmaUpdated = "APPRAISAL_PMA_UPDATED";
    public const string AppraisalStatusChanged = "APPRAISAL_STATUS_CHANGED";
    public const string DocumentFollowupRequired = "DOCUMENT_FOLLOWUP_REQUIRED";
    public const string QuotationCancelled = "QUOTATION_CANCELLED";
    public const string QuotationFinalized = "QUOTATION_FINALIZED";
    public const string QuotationReady = "QUOTATION_READY";
    public const string RequestRouteBack = "REQUEST_ROUTE_BACK";

    /// <summary>Sorted ordinally.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        AppraisalCancelled, AppraisalCompleted, AppraisalCreated, AppraisalPmaUpdated, AppraisalStatusChanged,
        DocumentFollowupRequired, QuotationCancelled, QuotationFinalized, QuotationReady, RequestRouteBack
    ];
}
