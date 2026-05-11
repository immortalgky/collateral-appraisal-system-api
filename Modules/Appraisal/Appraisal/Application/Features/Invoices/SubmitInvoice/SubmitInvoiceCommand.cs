namespace Appraisal.Application.Features.Invoices.SubmitInvoice;

public record SubmitInvoiceCommand(Guid InvoiceId, Guid CallerCompanyId)
    : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
