using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.OpenNegotiation;

public record OpenNegotiationCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    string Message)
    : ICommand<OpenNegotiationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
