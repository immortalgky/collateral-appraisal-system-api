namespace Appraisal.Application.Features.Invoices.BulkMarkInvoicesPaid;

public record BulkMarkInvoicesPaidCommand(
    Guid[] InvoiceIds,
    string ApprovedBy,
    string PaymentOrderNo,
    DateOnly PaidDate
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;
