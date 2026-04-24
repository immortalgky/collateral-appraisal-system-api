namespace Appraisal.Application.Features.Quotations.GetQuotations;

public record GetQuotationsQuery(PaginationRequest PaginationRequest, Guid? AppraisalId = null) : IQuery<GetQuotationsResult>;