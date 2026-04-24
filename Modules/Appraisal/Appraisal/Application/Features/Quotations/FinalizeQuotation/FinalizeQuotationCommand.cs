using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public record FinalizeQuotationCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    decimal FinalPrice,
    string? Reason = null)
    : ICommand<FinalizeQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
