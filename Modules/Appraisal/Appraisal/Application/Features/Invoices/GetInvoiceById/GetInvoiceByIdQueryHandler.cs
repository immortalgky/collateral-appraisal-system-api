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

        // Snapshot fields live on InvoiceItems; payment-state fields (Paid/Remaining/LastPaymentDate)
        // are read live from AppraisalFee + payment history so the detail screen mirrors the
        // current state shown on the eligible-assignments selector. Mirrors vw_EligibleAssignments.
        // BankAbsorb rows are excluded from PayPartialAmount, RemainingFee, and LastPaymentDate
        // because they are synthetic settlement rows, not real customer payments.
        const string itemsSql = """
            SELECT ii.Id, ii.AssignmentId, ii.AppraisalNumber, ii.CustomerName, ii.ProductType,
                   ii.FeeBeforeVAT, ii.VATRate, ii.VATAmount, ii.TotalFeeAfterVAT, ii.BankAbsorbAmount,
                   ISNULL((SELECT SUM(h.PaymentAmount)
                            FROM appraisal.AppraisalFeePaymentHistory h
                            WHERE h.AppraisalFeeId = af.Id
                              AND h.Source <> 'BankAbsorb'), 0)                            AS PayPartialAmount,
                   ISNULL(af.TotalFeeAfterVAT
                       - (SELECT ISNULL(SUM(h.PaymentAmount), 0)
                          FROM appraisal.AppraisalFeePaymentHistory h
                          WHERE h.AppraisalFeeId = af.Id
                            AND h.Source <> 'BankAbsorb')
                       - af.BankAbsorbAmount, 0)                                           AS RemainingFee,
                   (SELECT MAX(h.PaymentDate)
                    FROM appraisal.AppraisalFeePaymentHistory h
                    WHERE h.AppraisalFeeId = af.Id
                      AND h.Source <> 'BankAbsorb')                                        AS LastPaymentDate,
                   ii.SubmittedDate
            FROM appraisal.InvoiceItems ii
            LEFT JOIN appraisal.AppraisalFees af ON af.AssignmentId = ii.AssignmentId
            WHERE ii.InvoiceId = @InvoiceId
            ORDER BY ii.CreatedAt
            """;
        var items = (await connection.QueryAsync<InvoiceItemDto>(
            itemsSql,
            new { InvoiceId = request.InvoiceId })).ToList();

        return header with { Items = items };
    }
}
