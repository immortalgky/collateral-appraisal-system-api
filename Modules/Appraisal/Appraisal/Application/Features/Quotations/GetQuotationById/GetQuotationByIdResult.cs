namespace Appraisal.Application.Features.Quotations.GetQuotationById;

public record GetQuotationByIdResult(
    // ── Core RFQ fields ──────────────────────────────────────────────────────
    Guid Id,
    string? QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string Status,
    Guid RequestedBy,
    string RequestedByName,
    string? Description,
    string? SpecialRequirements,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    int TotalQuotationsReceived,

    // ── Legacy selection (non-IBG path) ──────────────────────────────────────
    Guid? SelectedCompanyId,
    Guid? SelectedQuotationId,
    DateTime? SelectedAt,
    string? SelectionReason,

    // ── IBG-extended fields ───────────────────────────────────────────────────
    // v2: AppraisalId scalar replaced with Appraisals array.
    // For backward compat FirstAppraisalId is still present (first element or null).
    IReadOnlyList<QuotationAppraisalResult> Appraisals,
    Guid? FirstAppraisalId,
    Guid? RequestId,
    Guid? WorkflowInstanceId,
    Guid? TaskExecutionId,
    string? BankingSegment,
    Guid? RmUserId,
    DateTime? SubmissionsClosedAt,
    DateTime? ShortlistSentToRmAt,
    Guid? ShortlistSentByAdminId,
    int TotalShortlisted,
    Guid? TentativeWinnerQuotationId,
    DateTime? TentativelySelectedAt,
    Guid? TentativelySelectedBy,
    string? TentativelySelectedByRole,

    // ── v4: shared documents ─────────────────────────────────────────────────
    IReadOnlyList<QuotationSharedDocumentResult> SharedDocuments,

    // ── Company quotations (filtered by role via QuotationAccessPolicy) ───────
    IReadOnlyList<CompanyQuotationResult> CompanyQuotations
);

/// <summary>
/// A document the admin has explicitly shared with invited external companies.
/// v7: enriched with display metadata resolved from request.RequestDocuments
/// / request.RequestTitleDocuments so ExtCompany FE doesn't need to hit the
/// anonymous /requests/{requestId}/documents endpoint.
/// </summary>
public record QuotationSharedDocumentResult(
    Guid AppraisalId,
    Guid DocumentId,
    string Level,
    string? FileName,
    string? FileType,
    string? DocumentTypeName);

/// <summary>
/// Represents a single appraisal entry in the quotation's N:M join collection.
/// C2: enriched with display fields from the denormalized QuotationRequestItem.
/// </summary>
public record QuotationAppraisalResult(
    Guid AppraisalId,
    DateTime AddedAt,
    string AddedBy,
    // C2: enriched fields — populated from matching QuotationRequestItem (denormalized at create-time)
    string? AppraisalNumber,
    string? PropertyType,
    string? Address,
    string? LoanType,
    // v7: the appraisal's owning request — FE uses this to call `/requests/{requestId}/documents`
    // when building the "share documents" picker.
    Guid? RequestId
);

public record CompanyQuotationResult(
    Guid Id,
    Guid CompanyId,
    string QuotationNumber,
    string Status,
    decimal TotalQuotedPrice,
    decimal? OriginalQuotedPrice,
    decimal? CurrentNegotiatedPrice,
    int NegotiationRounds,
    bool IsShortlisted,
    bool IsWinner,
    int EstimatedDays,
    DateTime? ValidUntil,
    DateTime? ProposedStartDate,
    DateTime? ProposedCompletionDate,
    string? Remarks,
    IReadOnlyList<CompanyQuotationItemResult> Items,
    IReadOnlyList<CompanyQuotationNegotiationResult> Negotiations
);

public record CompanyQuotationItemResult(
    Guid Id,
    Guid AppraisalId,
    int ItemNumber,
    decimal QuotedPrice,
    decimal OriginalQuotedPrice,
    decimal CurrentNegotiatedPrice,
    int EstimatedDays
);

public record CompanyQuotationNegotiationResult(
    Guid Id,
    int NegotiationRound,
    string InitiatedBy,
    DateTime InitiatedAt,
    decimal? CounterPrice,
    string Message,
    string Status,
    string? ResponseMessage,
    DateTime? RespondedAt
);
