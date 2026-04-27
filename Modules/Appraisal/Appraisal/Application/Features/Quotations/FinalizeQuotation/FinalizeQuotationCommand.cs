using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    string? Reason = null)
    : ICommand<FinalizeQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
