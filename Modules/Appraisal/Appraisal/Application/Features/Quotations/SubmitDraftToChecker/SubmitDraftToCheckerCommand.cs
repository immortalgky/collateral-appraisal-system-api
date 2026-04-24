using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SubmitDraftToChecker;

public record SubmitDraftToCheckerCommand(Guid QuotationRequestId, Guid CompanyId)
    : ICommand<SubmitDraftToCheckerResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
