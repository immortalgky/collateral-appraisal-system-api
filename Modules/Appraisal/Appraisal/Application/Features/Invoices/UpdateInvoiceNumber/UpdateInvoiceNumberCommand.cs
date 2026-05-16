namespace Appraisal.Application.Features.Invoices.UpdateInvoiceNumber;

public record UpdateInvoiceNumberCommand(Guid InvoiceId, Guid CallerCompanyId, string InvoiceNumber)
    : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
