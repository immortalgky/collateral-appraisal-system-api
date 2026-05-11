using Appraisal.Domain.Invoices;

namespace Appraisal.Application.Features.Invoices.ApproveInvoice;

public class ApproveInvoiceCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork)
    : ICommandHandler<ApproveInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(ApproveInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        invoice.Approve(
            request.ApprovedBy,
            request.PaymentReference,
            request.PaymentMethod,
            request.PaymentDate);

        repository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
