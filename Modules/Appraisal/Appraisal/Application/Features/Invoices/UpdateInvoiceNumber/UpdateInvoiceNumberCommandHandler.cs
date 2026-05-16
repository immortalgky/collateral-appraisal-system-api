using Appraisal.Domain.Invoices;
using Microsoft.Data.SqlClient;

namespace Appraisal.Application.Features.Invoices.UpdateInvoiceNumber;

public class UpdateInvoiceNumberCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork)
    : ICommandHandler<UpdateInvoiceNumberCommand, Guid>
{
    public async Task<Guid> Handle(UpdateInvoiceNumberCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        if (invoice.CompanyId != request.CallerCompanyId)
            throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        if (invoice.Status != "Pending")
            throw new BadRequestException("Invoice number can only be updated on Pending invoices.");

        invoice.SetInvoiceNumber(request.InvoiceNumber);

        repository.Update(invoice);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException($"Invoice number '{request.InvoiceNumber}' is already in use.");
        }

        return invoice.Id;
    }
}
