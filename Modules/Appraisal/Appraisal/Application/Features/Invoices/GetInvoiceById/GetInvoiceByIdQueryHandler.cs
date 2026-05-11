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

        var header = await connection.QueryFirstOrDefaultAsync<InvoiceDetailDto>(
            "SELECT * FROM appraisal.vw_InvoiceList WHERE Id = @Id",
            new { Id = request.InvoiceId });

        if (header is null) return null;

        if (request.CallerCompanyId.HasValue && header.CompanyId != request.CallerCompanyId.Value)
            return null;

        var items = (await connection.QueryAsync<InvoiceItemDto>(
            "SELECT * FROM appraisal.InvoiceItems WHERE InvoiceId = @InvoiceId ORDER BY CreatedAt",
            new { InvoiceId = request.InvoiceId })).ToList();

        return header with { Items = items };
    }
}
