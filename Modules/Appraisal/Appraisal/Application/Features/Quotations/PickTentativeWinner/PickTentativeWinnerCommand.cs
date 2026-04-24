using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public record PickTentativeWinnerCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    string? Reason = null,
    bool RequestNegotiation = false,
    string? NegotiationNote = null)
    : ICommand<PickTentativeWinnerResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
