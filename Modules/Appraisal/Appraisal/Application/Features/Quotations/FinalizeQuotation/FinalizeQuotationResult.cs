namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationResult(
    Guid QuotationRequestId,
    Guid WinningCompanyQuotationId,
    Guid WinningCompanyId,
    decimal FinalPrice,
    string Status);
