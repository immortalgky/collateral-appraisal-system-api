namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

public record GetMyDraftsForAssemblyResult(IReadOnlyList<QuotationDraftSummaryDto> Drafts);

public record QuotationDraftSummaryDto(
    Guid Id,
    string? QuotationNumber,
    DateTime RequestDate,
    DateTime CutOffTime,
    /// <summary>Distinct Banking Segments of the draft's current appraisals (computed live).</summary>
    IReadOnlyList<string> BankingSegment,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    /// <summary>Up to 5 appraisal numbers for preview in the picker.</summary>
    IReadOnlyList<string> AppraisalNumberPreview
);
