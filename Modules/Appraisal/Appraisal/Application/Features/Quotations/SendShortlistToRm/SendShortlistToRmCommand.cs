using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SendShortlistToRm;

public record SendShortlistToRmCommand(Guid QuotationRequestId)
    : ICommand<SendShortlistToRmResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
