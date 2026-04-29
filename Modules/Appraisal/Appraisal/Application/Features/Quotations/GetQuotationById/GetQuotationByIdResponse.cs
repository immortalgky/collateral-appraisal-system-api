namespace Appraisal.Application.Features.Quotations.GetQuotationById;

// Response mirrors Result 1:1 — Mapster identity mapping.
// Kept as a separate type to preserve the API contract layer.

public record GetQuotationByIdResponse(
    // ── Core RFQ fields ──────────────────────────────────────────────────────
    Guid Id,
    string? QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string Status,
    string RequestedBy,
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

    // ── Cancellation ──────────────────────────────────────────────────────────
    string? CancellationReason,

    // ── IBG-extended fields (v2) ──────────────────────────────────────────────
    // v2: AppraisalId scalar removed; replaced by Appraisals array.
    IReadOnlyList<QuotationAppraisalResult> Appraisals,
    Guid? FirstAppraisalId,
    Guid? RequestId,
    Guid? WorkflowInstanceId,
    Guid? TaskExecutionId,
    string? BankingSegment,
    Guid? RmUserId,
    string? RmUserName,
    string? RmUserFullName,
    DateTime? SubmissionsClosedAt,
    DateTime? ShortlistSentToRmAt,
    Guid? ShortlistSentByAdminId,
    int TotalShortlisted,
    Guid? TentativeWinnerQuotationId,
    DateTime? TentativelySelectedAt,
    Guid? TentativelySelectedBy,
    string? TentativelySelectedByRole,

    // ── v4: RM negotiation recommendation captured when RM picks a tentative winner ─
    bool RmRequestsNegotiation,
    string? RmNegotiationNote,

    // ── v4: shared documents ─────────────────────────────────────────────────
    IReadOnlyList<QuotationSharedDocumentResult> SharedDocuments,

    // ── Company quotations (filtered by role) ─────────────────────────────────
    IReadOnlyList<CompanyQuotationResult> CompanyQuotations,

    // ── Invited companies (non-Expired invitations, enriched with name) ───────
    IReadOnlyList<InvitedCompanyResult> InvitedCompanies,
    bool CanEdit,
    bool CanPickWinner
);