namespace Integration.Application.Features.Quotations.SelectQuotation;

public record SelectQuotationRequest(
    string CompanyQuotationId,
    string RmUsername,
    bool RequestNegotiation = false,
    string? NegotiationNote = null);

public record SelectQuotationResponse(string Status, string Message);
