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
///
/// <see cref="ItemNegotiationReason"/> is required whenever <see cref="NegotiatedDiscount"/>
/// resolves to $0 (or null) — the company's explanation for offering no further discount on
/// this item this round.
/// </summary>
public record RespondNegotiationItemRequest(
    Guid AppraisalId,
    decimal? NegotiatedDiscount,
    string? ItemNegotiationReason = null);
