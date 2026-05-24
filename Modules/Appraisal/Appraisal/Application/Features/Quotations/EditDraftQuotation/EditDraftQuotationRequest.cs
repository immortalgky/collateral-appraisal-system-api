namespace Appraisal.Application.Features.Quotations.EditDraftQuotation;

public record EditDraftQuotationRequest(
    DateTime CutOffTime,
    IReadOnlyList<Guid> CompanyIds,
    IReadOnlyList<EditDraftAppraisalEntry>? Appraisals);
