namespace Appraisal.Application.Features.Quotations.OpenNegotiation;

public record OpenNegotiationRequest(
    Guid CompanyQuotationId,
    decimal ProposedPrice,
    string Message);
