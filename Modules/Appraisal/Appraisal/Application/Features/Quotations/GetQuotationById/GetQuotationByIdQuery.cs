namespace Appraisal.Application.Features.Quotations.GetQuotationById;

public record GetQuotationByIdQuery(Guid Id) : IQuery<GetQuotationByIdResult>;