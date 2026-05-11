using Appraisal.Application.Configurations;
using Appraisal.Application.Features.Quotations.Shared;

namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public record PickTentativeWinnerCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    QuotationActor Actor,
    string? Reason = null,
    bool RequestNegotiation = false,
    string? NegotiationNote = null)
    : ICommand<PickTentativeWinnerResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
