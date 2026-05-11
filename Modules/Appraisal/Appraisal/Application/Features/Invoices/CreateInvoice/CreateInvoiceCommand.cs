namespace Appraisal.Application.Features.Invoices.CreateInvoice;

public record CreateInvoiceCommand(
    Guid CompanyId,
    Guid[] AssignmentIds,
    string? Notes
) : ICommand<Guid>, ITransactionalCommand<IAppraisalUnitOfWork>;
