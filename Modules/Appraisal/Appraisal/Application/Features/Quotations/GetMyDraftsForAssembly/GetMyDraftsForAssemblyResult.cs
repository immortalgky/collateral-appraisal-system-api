namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

public record GetMyDraftsForAssemblyResult(IReadOnlyList<QuotationDraftSummaryDto> Drafts);

public record QuotationDraftSummaryDto(
    Guid Id,
    string? QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string? BankingSegment,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    /// <summary>Up to 5 appraisal numbers for preview in the picker.</summary>
    IReadOnlyList<string> AppraisalNumberPreview
);
