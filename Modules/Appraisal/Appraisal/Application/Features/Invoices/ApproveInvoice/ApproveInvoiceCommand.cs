namespace Appraisal.Application.Features.Invoices.ApproveInvoice;

public record ApproveInvoiceCommand(
    Guid InvoiceId,
    string ApprovedBy,
    string? PaymentReference,
    string? PaymentMethod,
    DateOnly? PaymentDate
) : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
