using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.RespondNegotiation;

/// <summary>
/// Verb: Accept | Counter | Reject
/// </summary>
public record RespondNegotiationCommand(
    Guid QuotationRequestId,
    Guid NegotiationId,
    Guid CompanyQuotationId,
    string Verb,
    decimal? CounterPrice = null,
    string? Message = null)
    : ICommand<RespondNegotiationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
