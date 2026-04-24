using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.CancelQuotation;

public record CancelQuotationCommand(Guid QuotationRequestId, string? Reason = null)
    : ICommand<CancelQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
