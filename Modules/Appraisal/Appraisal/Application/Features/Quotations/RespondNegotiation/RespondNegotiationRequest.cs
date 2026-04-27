namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

public record RespondNegotiationRequest(
    Guid CompanyQuotationId,
    string Verb,
    decimal? CounterPrice = null,
    string? Message = null,
    List<RespondNegotiationItemRequest>? Items = null);

/// <summary>
/// Per-appraisal negotiated discount supplied with a Counter response.
/// When present, the backend updates each item's NegotiatedDiscount and recomputes
/// the total — CounterPrice on the parent request is ignored.
/// </summary>
public record RespondNegotiationItemRequest(
    Guid AppraisalId,
    decimal? NegotiatedDiscount);
