using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Invoices;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.Time;

namespace Appraisal.Application.Features.Invoices.MarkInvoicePaid;

public class MarkInvoicePaidCommandHandler(
    IInvoiceRepository repository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkInvoicePaidCommand, Guid>
{
    public async Task<Guid> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdWithItemsAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        invoice.MarkAsPaid(
            request.ApprovedBy,
            request.PaymentOrderNo,
            request.PaidDate,
            dateTimeProvider.ApplicationNow);

        repository.Update(invoice);

        // Settle the bank-absorbed portion on each linked fee
        var feeIds = invoice.Items.Select(i => i.AppraisalFeeId).Distinct().ToList();
        var fees = await dbContext.AppraisalFees
            .Include(f => f.PaymentHistory)
            .Where(f => feeIds.Contains(f.Id))
            .ToListAsync(cancellationToken);

        var paidDateTime = invoice.PaidDate!.Value.ToDateTime(TimeOnly.MinValue);
        var reference = invoice.PaymentOrderNo ?? invoice.InvoiceNumber;

        foreach (var fee in fees)
        {
            fee.SettleBankAbsorbed(paidDateTime, reference);
            dbContext.AppraisalFees.Update(fee);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
