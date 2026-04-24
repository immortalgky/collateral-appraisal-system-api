using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.AutoDeclineCompanyQuotation;

/// <summary>
/// System-initiated decline for a company that did not respond by the quotation DueDate.
/// Creates or transitions the CompanyQuotation to Declined and marks the invitation Expired.
/// </summary>
public record AutoDeclineCompanyQuotationCommand(
    Guid QuotationRequestId,
    Guid CompanyId,
    string Reason
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;
