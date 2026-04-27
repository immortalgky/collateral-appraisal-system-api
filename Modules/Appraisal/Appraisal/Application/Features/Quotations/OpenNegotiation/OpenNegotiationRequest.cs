namespace Appraisal.Application.Features.Quotations.OpenNegotiation;

public record OpenNegotiationRequest(
    Guid CompanyQuotationId,
    string Message);
