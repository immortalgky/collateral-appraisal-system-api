using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Invoices;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.Time;

namespace Appraisal.Application.Features.Invoices.BulkMarkInvoicesPaid;

public class BulkMarkInvoicesPaidCommandHandler(
    IInvoiceRepository repository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BulkMarkInvoicesPaidCommand, Unit>
{
    public async Task<Unit> Handle(BulkMarkInvoicesPaidCommand request, CancellationToken cancellationToken)
    {
        if (request.InvoiceIds.Length == 0)
            throw new BadRequestException("At least one invoice ID is required.");

        var invoices = await repository.GetByIdsWithItemsAsync(request.InvoiceIds, cancellationToken);

        var missingIds = request.InvoiceIds.Except(invoices.Select(i => i.Id)).ToList();
        if (missingIds.Count > 0)
            throw new NotFoundException($"Invoice(s) not found: {string.Join(", ", missingIds)}");

        var nonSentInvoices = invoices.Where(i => i.Status != "Sent").ToList();
        if (nonSentInvoices.Count > 0)
            throw new BadRequestException(
                $"All invoices must have status 'Sent'. The following invoices have a different status: {string.Join(", ", nonSentInvoices.Select(i => i.Id))}");

        var distinctCompanies = invoices.Select(i => i.CompanyId).Distinct().ToList();
        if (distinctCompanies.Count > 1)
            throw new BadRequestException("All invoices in a bulk payment must belong to the same company.");

        // Pre-load all fees so EF tracks them in the same DbContext instance
        var allFeeIds = invoices.SelectMany(i => i.Items).Select(ii => ii.AppraisalFeeId).Distinct().ToList();
        var fees = await dbContext.AppraisalFees
            .Include(f => f.PaymentHistory)
            .Where(f => allFeeIds.Contains(f.Id))
            .ToListAsync(cancellationToken);
        var feeById = fees.ToDictionary(f => f.Id);

        var approvedAt = dateTimeProvider.ApplicationNow;
        foreach (var invoice in invoices)
        {
            invoice.MarkAsPaid(request.ApprovedBy, request.PaymentOrderNo, request.PaidDate, approvedAt);
            repository.Update(invoice);

            var paidDateTime = invoice.PaidDate!.Value.ToDateTime(TimeOnly.MinValue);
            var reference = invoice.PaymentOrderNo ?? invoice.InvoiceNumber;

            foreach (var item in invoice.Items)
            {
                if (!feeById.TryGetValue(item.AppraisalFeeId, out var fee))
                    continue;

                fee.SettleBankAbsorbed(paidDateTime, reference);
                dbContext.AppraisalFees.Update(fee);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
