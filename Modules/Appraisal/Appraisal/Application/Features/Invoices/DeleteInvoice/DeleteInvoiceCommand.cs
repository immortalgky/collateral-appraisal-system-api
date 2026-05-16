namespace Appraisal.Application.Features.Invoices.DeleteInvoice;

public record DeleteInvoiceCommand(Guid InvoiceId, Guid CallerCompanyId)
    : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;
