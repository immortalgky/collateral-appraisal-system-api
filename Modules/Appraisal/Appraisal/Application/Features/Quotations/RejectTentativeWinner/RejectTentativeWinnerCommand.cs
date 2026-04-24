using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.RejectTentativeWinner;

public record RejectTentativeWinnerCommand(Guid QuotationRequestId, string Reason)
    : ICommand<RejectTentativeWinnerResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
