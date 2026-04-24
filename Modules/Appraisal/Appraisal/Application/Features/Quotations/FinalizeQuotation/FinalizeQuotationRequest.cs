namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationRequest(
    Guid CompanyQuotationId,
    decimal FinalPrice,
    string? Reason = null);
