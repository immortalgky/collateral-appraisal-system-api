using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SelectQuotationFromIntegration;

public record SelectQuotationFromIntegrationCommand(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    string RmUsername,
    bool RequestNegotiation = false,
    string? NegotiationNote = null)
    : ICommand<SelectQuotationFromIntegrationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record SelectQuotationFromIntegrationResult(Guid QuotationRequestId, Guid CompanyQuotationId, string Status);
