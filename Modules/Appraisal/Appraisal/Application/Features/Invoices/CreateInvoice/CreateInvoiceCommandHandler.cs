using Appraisal.Domain.Invoices;
using Dapper;

namespace Appraisal.Application.Features.Invoices.CreateInvoice;

public class CreateInvoiceCommandHandler(
    IInvoiceRepository repository,
    IAppraisalUnitOfWork unitOfWork,
    ISqlConnectionFactory sqlConnectionFactory)
    : ICommandHandler<CreateInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", request.CompanyId);
        parameters.Add("AssignmentIds", request.AssignmentIds);

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
              )
            """,
            parameters)).ToList();

        var foundIds = eligibleRows.Select(r => r.AssignmentId).ToHashSet();
        var missingIds = request.AssignmentIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
            throw new BadRequestException($"Assignment(s) not eligible for invoicing: {string.Join(", ", missingIds)}");

        var invoice = Invoice.Create(request.CompanyId);

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

        if (request.Notes is not null)
            invoice.SetNotes(request.Notes);

        await repository.AddAsync(invoice, cancellationToken);
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
