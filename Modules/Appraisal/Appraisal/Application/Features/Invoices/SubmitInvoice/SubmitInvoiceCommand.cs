namespace Appraisal.Application.Features.Invoices.SubmitInvoice;

public record SubmitInvoiceCommand(Guid InvoiceId, Guid CallerCompanyId, string InvoiceNumber)
    : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
