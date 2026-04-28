using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.RecallShortlist;

public record RecallShortlistCommand(Guid QuotationRequestId)
    : ICommand<RecallShortlistResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
