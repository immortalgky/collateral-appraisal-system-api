using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Integration.Application.Services;

public class QuotationFinalizeLookupService(
    ISqlConnectionFactory connectionFactory,
    ILogger<QuotationFinalizeLookupService> logger) : IQuotationFinalizeLookupService
{
    // QuotationNumber comes from the QuotationRequest (RFQ) — CompanyQuotation.QuotationNumber
    // is not populated. Same source the get-quotation-valuers API uses.
    private const string SqlHeader = """
        SELECT
            qr.QuotationNumber AS QuotationNumber,
            cq.Id AS CompanyQuotationId,
            c.Name AS ValuerName,
            COALESCE(cq.CurrentNegotiatedPrice, cq.TotalQuotedPrice) AS TotalAppraisalFee,
            cq.EstimatedDays AS EstimatedDays
        FROM appraisal.CompanyQuotations cq
        JOIN auth.Companies c ON c.Id = cq.CompanyId
        JOIN appraisal.QuotationRequests qr ON qr.Id = cq.QuotationRequestId
        WHERE cq.Id = @WinningQuotationId
        """;

    // QuotedPrice mirrors how CompanyQuotation.TotalQuotedPrice is built (VAT-inclusive net
    // after discounts, falling back to the legacy QuotedPrice when FeeAmount is unset), so the
    // per-item values sum to the header totalAppraisalFee. PropertyType is resolved via a TOP 1
    // subquery — joining QuotationRequestItems directly would fan out duplicate rows since there
    // is no unique constraint on (QuotationRequestId, AppraisalId).
    private const string SqlItems = """
        SELECT
            a.AppraisalNumber AS AppraisalNumber,
            CASE WHEN cqi.FeeAmount > 0
                 THEN (cqi.FeeAmount - cqi.Discount - ISNULL(cqi.NegotiatedDiscount, 0))
                      * (1.0 + cqi.VatPercent / 100.0)
                 ELSE cqi.QuotedPrice
            END AS QuotedPrice,
            (SELECT TOP 1 qri.PropertyType
             FROM appraisal.QuotationRequestItems qri
             WHERE qri.QuotationRequestId = @QuotationRequestId
               AND qri.AppraisalId = cqi.AppraisalId
             ORDER BY qri.ItemNumber) AS PropertyType,
            cqi.EstimatedDays AS EstimatedDays
        FROM appraisal.CompanyQuotationItems cqi
        JOIN appraisal.Appraisals a ON a.Id = cqi.AppraisalId
        WHERE cqi.CompanyQuotationId = @WinningQuotationId
        ORDER BY cqi.ItemNumber
        """;

    public async Task<QuotationFinalizeSnapshot?> GetSnapshotAsync(
        Guid quotationRequestId,
        Guid winningQuotationId,
        CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("QuotationRequestId", quotationRequestId);
        parameters.Add("WinningQuotationId", winningQuotationId);

        var header = await connection.QuerySingleOrDefaultAsync<HeaderRow>(
            new CommandDefinition(SqlHeader, parameters, cancellationToken: ct));

        if (header is null)
        {
            logger.LogWarning(
                "QuotationFinalizeLookupService: no CompanyQuotation found for WinningQuotationId {WinningQuotationId} (QuotationRequestId {QuotationRequestId})",
                winningQuotationId, quotationRequestId);
            return null;
        }

        var itemRows = await connection.QueryAsync<ItemRow>(
            new CommandDefinition(SqlItems, parameters, cancellationToken: ct));

        var items = itemRows
            .Select(r => new QuotationFinalizeItem(r.AppraisalNumber, r.QuotedPrice, r.PropertyType, r.EstimatedDays))
            .ToList();

        return new QuotationFinalizeSnapshot(
            header.QuotationNumber,
            header.CompanyQuotationId,
            header.ValuerName,
            header.TotalAppraisalFee,
            header.EstimatedDays,
            items);
    }

    private sealed record HeaderRow(
        string? QuotationNumber,
        Guid CompanyQuotationId,
        string? ValuerName,
        decimal TotalAppraisalFee,
        int EstimatedDays);

    private sealed record ItemRow(
        string AppraisalNumber,
        decimal QuotedPrice,
        string? PropertyType,
        int EstimatedDays);
}
