namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsResult(PaginatedResult<QuotationDto> Quotations);

public record QuotationDto(
    Guid Id,
    string QuotationNumber,
    DateTime RequestDate,
    DateTime DueDate,
    string Status,
    string RequestedByName,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    int TotalQuotationsReceived);
