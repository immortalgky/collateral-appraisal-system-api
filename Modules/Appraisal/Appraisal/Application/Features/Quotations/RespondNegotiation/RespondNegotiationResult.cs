namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

public record RespondNegotiationResult(
    Guid QuotationRequestId,
    string QuotationStatus,
    string NegotiationStatus);
