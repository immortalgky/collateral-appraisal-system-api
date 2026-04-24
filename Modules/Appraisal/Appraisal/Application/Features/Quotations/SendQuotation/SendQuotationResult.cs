namespace Appraisal.Application.Features.Quotations.SendQuotation;

public record SendQuotationResult(
    Guid QuotationRequestId,
    string Status,
    int TotalAppraisals,
    int TotalCompaniesInvited);
