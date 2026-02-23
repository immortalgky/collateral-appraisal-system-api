namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsQuery(PaginationRequest PaginationRequest) : IQuery<GetQuotationsResult>;