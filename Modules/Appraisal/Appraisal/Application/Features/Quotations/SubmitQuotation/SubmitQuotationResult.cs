namespace Appraisal.Application.Features.Quotations.SubmitQuotation;

public record SubmitQuotationResult(
    Guid CompanyQuotationId,
    string QuotationNumber,
    decimal TotalQuotedPrice,
    string Status);
