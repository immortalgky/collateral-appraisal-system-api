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

        if (invoice.Status != "Pending")
            throw new BadRequestException("Only Pending invoices can be updated.");

        if (invoice.CompanyId != request.CallerCompanyId)
            throw new NotFoundException($"Invoice '{request.InvoiceId}' not found.");

        var connection = sqlConnectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", request.CallerCompanyId);
        parameters.Add("AssignmentIds", request.AssignmentIds);
        parameters.Add("CurrentInvoiceId", request.InvoiceId);

        // Exclude items already on another invoice; allow items already on the current draft
        // so they're preserved through the ClearItems + re-add cycle below.
        var eligibleRows = (await connection.QueryAsync<EligibleRow>(
            """
            SELECT v.AssignmentId, v.AppraisalFeeId, v.AppraisalNumber, v.CustomerName, v.ProductType,
                   v.FeeBeforeVAT, v.VATRate, v.VATAmount, v.TotalFeeAfterVAT, v.BankAbsorbAmount,
                   v.SubmittedDate
            FROM appraisal.vw_EligibleAssignments v
            WHERE v.AssigneeCompanyId = @CompanyId
              AND v.AssignmentId IN @AssignmentIds
              AND NOT EXISTS (
                  SELECT 1 FROM appraisal.InvoiceItems ii
                  WHERE ii.AssignmentId = v.AssignmentId
                    AND ii.InvoiceId <> @CurrentInvoiceId
              )
            """,
            parameters)).ToList();

        var foundIds = eligibleRows.Select(r => r.AssignmentId).ToHashSet();
        var missingIds = request.AssignmentIds.Where(id => !foundIds.Contains(id)).ToList();
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
                row.SubmittedDate);
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
        DateTime? SubmittedDate);
}
