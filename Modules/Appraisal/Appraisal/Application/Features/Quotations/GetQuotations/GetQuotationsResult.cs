namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsResult(PaginatedResult<QuotationDto> Quotations);