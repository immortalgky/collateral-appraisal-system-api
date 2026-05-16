namespace Appraisal.Application.Features.Invoices.MarkInvoicePaid;

public record MarkInvoicePaidCommand(
    Guid InvoiceId,
    string ApprovedBy,
    string PaymentOrderNo,
    DateOnly PaidDate
) : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
