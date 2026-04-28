namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationRequest(
    Guid CompanyQuotationId,
    string? Reason = null);
