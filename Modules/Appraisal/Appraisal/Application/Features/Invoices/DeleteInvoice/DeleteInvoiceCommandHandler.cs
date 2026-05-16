using Appraisal.Domain.Invoices;

namespace Appraisal.Application.Features.Invoices.DeleteInvoice;

public class DeleteInvoiceCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork)
    : ICommandHandler<DeleteInvoiceCommand, Unit>
{
    public async Task<Unit> Handle(DeleteInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        if (invoice.CompanyId != request.CallerCompanyId)
            throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        if (invoice.Status != "Pending")
            throw new BadRequestException("Only Pending invoices can be deleted.");

        repository.Remove(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
