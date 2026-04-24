namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

public record RespondNegotiationRequest(
    Guid CompanyQuotationId,
    string Verb,
    decimal? CounterPrice = null,
    string? Message = null);
