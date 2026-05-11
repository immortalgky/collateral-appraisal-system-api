using Appraisal.Domain.Invoices;
using Dapper;

namespace Appraisal.Application.Features.Invoices.UpdateInvoiceDraft;

public class UpdateInvoiceDraftCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork,
    ISqlConnectionFactory sqlConnectionFactory)
    : ICommandHandler<UpdateInvoiceDraftCommand, Guid>
{
    public async Task<Guid> Handle(UpdateInvoiceDraftCommand request, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdWithItemsAsync(request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        if (invoice.Status != "Draft")
            throw new BadRequestException("Only Draft invoices can be updated.");

        if (invoice.CompanyId != request.CallerCompanyId)
            throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        var connection = sqlConnectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", request.CallerCompanyId.ToString());
        parameters.Add("AssignmentIds", request.AssignmentIds);

        var eligibleRows = (await connection.QueryAsync<EligibleRow>(
            """
            SELECT AssignmentId, AppraisalFeeId, AppraisalNumber, CustomerName, ProductType,
                   FeeBeforeVAT, VATRate, VATAmount, TotalFeeAfterVAT, BankAbsorbAmount, ReceivedDate
            FROM appraisal.vw_EligibleAssignments
            WHERE AssigneeCompanyId = @CompanyId
              AND AssignmentId IN @AssignmentIds
            """,
            parameters)).ToList();

        var currentItemAssignmentIds = invoice.Items.Select(i => i.AssignmentId).ToHashSet();

        var foundIds = eligibleRows.Select(r => r.AssignmentId).ToHashSet();
        var missingIds = request.AssignmentIds
            .Where(id => !foundIds.Contains(id) && !currentItemAssignmentIds.Contains(id))
            .ToList();
        if (missingIds.Count > 0)
            throw new BadRequestException($"Assignment(s) not eligible for invoicing: {string.Join(", ", missingIds)}");

        invoice.ClearItems();

        foreach (var row in eligibleRows)
        {
            invoice.AddItem(
                row.AssignmentId,
                row.AppraisalFeeId,
                row.BankAbsorbAmount,
                row.AppraisalNumber,
                row.CustomerName,
                row.ProductType,
                row.FeeBeforeVAT,
                row.VATRate,
                row.VATAmount,
                row.TotalFeeAfterVAT,
                row.ReceivedDate);
        }

        invoice.SetNotes(request.Notes);

        repository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }

    private record EligibleRow(
        Guid AssignmentId,
        Guid AppraisalFeeId,
        string? AppraisalNumber,
        string? CustomerName,
        string? ProductType,
        decimal FeeBeforeVAT,
        decimal VATRate,
        decimal VATAmount,
        decimal TotalFeeAfterVAT,
        decimal BankAbsorbAmount,
        DateTime? ReceivedDate);
}
