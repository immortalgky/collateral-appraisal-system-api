using Appraisal.Domain.Invoices;
using Microsoft.Data.SqlClient;

namespace Appraisal.Application.Features.Invoices.SubmitInvoice;

public class SubmitInvoiceCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork)
    : ICommandHandler<SubmitInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(SubmitInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdWithItemsAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        if (invoice.CompanyId != request.CallerCompanyId)
            throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        invoice.SetInvoiceNumber(request.InvoiceNumber);
        invoice.Submit();

        repository.Update(invoice);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateInvoiceNumber(ex))
        {
            throw new ConflictException($"Invoice number '{request.InvoiceNumber}' is already in use.");
        }

        return invoice.Id;
    }

    private static bool IsDuplicateInvoiceNumber(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };
}
