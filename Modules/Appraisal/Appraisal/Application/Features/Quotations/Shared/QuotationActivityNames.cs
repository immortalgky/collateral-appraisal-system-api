namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Canonical activity names written to QuotationActivityLogs.
/// All writers MUST use these constants — drift would break activity feed grouping/i18n.
/// </summary>
public static class QuotationActivityNames
{
    // Existing (preserve string values verbatim — already in the DB)
    public const string QuotationCreatedFromTask = "Quotation creation";
    public const string SubmittedToChecker = "Submitted to Checker";
    public const string QuotationSubmitted = "Quotation submitted";
    public const string InvitationDeclined = "Invitation declined";

    // New — admin lifecycle
    public const string QuotationCreated = "Quotation created";
    public const string QuotationSentToCompanies = "Sent to companies";
    public const string QuotationClosed = "Quotation closed";
    public const string QuotationCancelled = "Quotation cancelled";

    // New — selection
    public const string QuotationShortlisted = "Quotation shortlisted";
    public const string QuotationUnshortlisted = "Quotation unshortlisted";
    public const string ShortlistSentToRm = "Shortlist sent to RM";
    public const string ShortlistRecalled = "Shortlist recalled";
    public const string TentativeWinnerPicked = "Tentative winner picked";
    public const string TentativeWinnerRejected = "Tentative winner rejected";
    public const string QuotationFinalized = "Quotation finalized";

    // New — negotiation
    public const string NegotiationOpened = "Negotiation opened";
    public const string NegotiationResponded = "Negotiation responded";

    // New — system
    public const string InvitationAutoDeclined = "Invitation auto-declined";
}
