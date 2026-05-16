using Dapper;

namespace Appraisal.Application.Features.Invoices.GetInvoiceById;

public class GetInvoiceByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetInvoiceByIdQuery, InvoiceDetailDto?>
{
    public async Task<InvoiceDetailDto?> Handle(
        GetInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();

        const string headerSql = """
            SELECT Id, InvoiceNumber, Status, TotalAmount, CompanyName,
                   BankAccountNo, BankAccountName, Notes,
                   PaymentOrderNo, PaidDate, ApprovedBy, ApprovedAt,
                   SubmittedAt, CompanyId
            FROM appraisal.vw_InvoiceList
            WHERE Id = @Id
            """;
        var header = await connection.QueryFirstOrDefaultAsync<InvoiceDetailDto>(
            headerSql,
            new { Id = request.InvoiceId });

        if (header is null) return null;

        if (request.CallerCompanyId.HasValue && header.CompanyId != request.CallerCompanyId.Value)
            return null;

        const string itemsSql = """
            SELECT Id, AssignmentId, AppraisalNumber, CustomerName, ProductType,
                   FeeBeforeVAT, VATRate, VATAmount, TotalFeeAfterVAT, BankAbsorbAmount,
                   SubmittedDate
            FROM appraisal.InvoiceItems
            WHERE InvoiceId = @InvoiceId
            ORDER BY CreatedAt
            """;
        var items = (await connection.QueryAsync<InvoiceItemDto>(
            itemsSql,
            new { InvoiceId = request.InvoiceId })).ToList();

        return header with { Items = items };
    }
}
