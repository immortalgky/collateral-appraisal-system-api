namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public record PickTentativeWinnerRequest(
    Guid CompanyQuotationId,
    string? Reason = null,
    bool RequestNegotiation = false,
    string? NegotiationNote = null);
