using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.ShortlistQuotation;

public record ShortlistQuotationCommand(Guid QuotationRequestId, Guid CompanyQuotationId)
    : ICommand<ShortlistQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
