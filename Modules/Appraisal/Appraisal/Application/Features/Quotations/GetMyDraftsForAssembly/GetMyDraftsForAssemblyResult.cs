namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

public record GetMyDraftsForAssemblyResult(IReadOnlyList<QuotationDraftSummaryDto> Drafts);

public record QuotationDraftSummaryDto(
    Guid Id,
    string? QuotationNumber,
    DateTime RequestDate,
    DateTime CutOffTime,
    /// <summary>Legacy: segment stamped at creation. Not a boundary any more — use SegmentSet.</summary>
    string? BankingSegment,
    /// <summary>Distinct Banking Segments of the appraisals currently in the draft (derived live).</summary>
    IReadOnlyList<string> SegmentSet,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    /// <summary>Up to 5 appraisal numbers for preview in the picker.</summary>
    IReadOnlyList<string> AppraisalNumberPreview
);
