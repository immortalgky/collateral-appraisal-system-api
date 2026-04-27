using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

/// <summary>
/// Verb: Accept | Counter | Reject.
///
/// For Counter, the company should send <see cref="Items"/> describing per-appraisal
/// negotiated discounts. The backend recomputes TotalQuotedPrice from those items,
/// so <see cref="CounterPrice"/> is ignored when items are present and is kept only
/// for legacy callers that submit a single total.
/// </summary>
public record RespondNegotiationCommand(
    Guid QuotationRequestId,
    Guid NegotiationId,
    Guid CompanyQuotationId,
    string Verb,
    decimal? CounterPrice = null,
    string? Message = null,
    IReadOnlyList<RespondNegotiationItemRequest>? Items = null)
    : ICommand<RespondNegotiationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
