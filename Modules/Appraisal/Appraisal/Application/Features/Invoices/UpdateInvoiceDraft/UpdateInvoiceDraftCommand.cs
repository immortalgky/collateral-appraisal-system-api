namespace Appraisal.Application.Features.Invoices.UpdateInvoiceDraft;

public record UpdateInvoiceDraftCommand(
    Guid InvoiceId,
    Guid CallerCompanyId,
    Guid[] AssignmentIds,
    string? Notes
) : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
