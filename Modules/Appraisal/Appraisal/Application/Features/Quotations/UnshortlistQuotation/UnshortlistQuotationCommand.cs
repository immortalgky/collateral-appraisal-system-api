using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.UnshortlistQuotation;

public record UnshortlistQuotationCommand(Guid QuotationRequestId, Guid CompanyQuotationId)
    : ICommand<UnshortlistQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
