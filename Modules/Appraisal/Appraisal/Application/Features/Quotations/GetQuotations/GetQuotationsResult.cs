namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsResult(PaginatedResult<QuotationDto> Quotations);

public record QuotationDto(
    Guid Id,
    string QuotationNumber,
    DateTime RequestDate,
    DateTime CutOffTime,
    string Status,
    string RequestedBy,
    int TotalAppraisals,
    int TotalCompaniesInvited,
    int TotalQuotationsReceived,
    string? CustomerName,
    int CustomerCount,
    string? CustomerNames);
