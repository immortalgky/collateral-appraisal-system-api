namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsResponse(PaginatedResult<QuotationDto> Quotations);