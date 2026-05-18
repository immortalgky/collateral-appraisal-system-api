using Appraisal.Domain.Invoices;
using Shared.Time;

namespace Appraisal.Application.Features.Invoices.MarkInvoicePaid;

public class MarkInvoicePaidCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkInvoicePaidCommand, Guid>
{
    public async Task<Guid> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        invoice.MarkAsPaid(
            request.ApprovedBy,
            request.PaymentOrderNo,
            request.PaidDate,
            dateTimeProvider.ApplicationNow);

        repository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
