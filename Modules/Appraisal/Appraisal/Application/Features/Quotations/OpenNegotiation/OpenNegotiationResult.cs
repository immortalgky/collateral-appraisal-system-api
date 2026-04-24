namespace Appraisal.Application.Features.Quotations.OpenNegotiation;

public record OpenNegotiationResult(
    Guid QuotationRequestId,
    Guid NegotiationId,
    int NegotiationRound,
    string Status);
