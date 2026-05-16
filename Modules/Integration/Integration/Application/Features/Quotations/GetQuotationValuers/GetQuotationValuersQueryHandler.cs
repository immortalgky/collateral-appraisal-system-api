using Dapper;
using Shared.CQRS;

namespace Integration.Application.Features.Quotations.GetQuotationValuers;

public class GetQuotationValuersQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetQuotationValuersQuery, GetQuotationValuersResult>
{
    private const string SqlQuotationNumber = """
                                              SELECT QuotationNumber
                                              FROM appraisal.QuotationRequests
                                              WHERE Id = @QuotationId
                                              """;

    private const string SqlAppraisals = """
                                         SELECT qi.AppraisalNumber, qi.PropertyType, qi.PropertyLocation
                                         FROM appraisal.QuotationRequestItems qi
                                         WHERE qi.QuotationRequestId = @QuotationId
                                         ORDER BY qi.ItemNumber
                                         """;

    private const string SqlShortlistedCompanies = """
                                                   SELECT cq.Id AS CompanyQuotationId, c.Name AS CompanyName, cq.TotalQuotedPrice, cq.EstimatedDays 
                                                   FROM appraisal.CompanyQuotations cq
                                                   JOIN auth.Companies c ON c.Id = cq.CompanyId
                                                   WHERE cq.QuotationRequestId = @QuotationId AND cq.IsShortlisted = 1
                                                   """;

    private const string SqlFeeItems = """
                                       SELECT cqi.CompanyQuotationId,
                                              a.AppraisalNumber,
                                              (cqi.FeeAmount - cqi.Discount - ISNULL(cqi.NegotiatedDiscount, 0))
                                                * (1.0 + cqi.VatPercent / 100.0) AS QuotedPrice,
                                              cqi.EstimatedDays
                                       FROM appraisal.CompanyQuotationItems cqi
                                       JOIN appraisal.Appraisals a ON a.Id = cqi.AppraisalId
                                       WHERE cqi.CompanyQuotationId IN @CompanyQuotationIds
                                       """;

    public async Task<GetQuotationValuersResult> Handle(
        GetQuotationValuersQuery query,
        CancellationToken cancellationToken)
    {
        var conn = connectionFactory.GetOpenConnection();

        var appraisalParams = new DynamicParameters();
        appraisalParams.Add("QuotationId", query.QuotationId);

        var quotationNumber = await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(SqlQuotationNumber, appraisalParams, cancellationToken: cancellationToken));

        var appraisalRows = await conn.QueryAsync<AppraisalRow>(
            new CommandDefinition(SqlAppraisals, appraisalParams, cancellationToken: cancellationToken));

        var appraisals = appraisalRows
            .Select(r => new QuotationAppraisalItem(r.AppraisalNumber, r.PropertyType, r.PropertyLocation))
            .ToList();

        var companyParams = new DynamicParameters();
        companyParams.Add("QuotationId", query.QuotationId);

        var companyRows = await conn.QueryAsync<CompanyRow>(
            new CommandDefinition(SqlShortlistedCompanies, companyParams, cancellationToken: cancellationToken));

        var companies = companyRows.ToList();

        if (companies.Count == 0)
            return new GetQuotationValuersResult(query.QuotationId, quotationNumber, appraisals, []);

        var shortlistedIds = companies.Select(c => c.CompanyQuotationId).ToList();

        var feeParams = new DynamicParameters();
        feeParams.Add("CompanyQuotationIds", shortlistedIds);

        var feeRows = await conn.QueryAsync<FeeRow>(
            new CommandDefinition(SqlFeeItems, feeParams, cancellationToken: cancellationToken));

        var feesByCompany = feeRows
            .GroupBy(f => f.CompanyQuotationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var valuers = companies
            .Select(c => new QuotationValuerItem(
                c.CompanyQuotationId.ToString(),
                c.CompanyName,
                c.TotalQuotedPrice,
                c.EstimatedDays,
                feesByCompany.TryGetValue(c.CompanyQuotationId, out var items)
                    ? items.Select(i => new ValuerAppraisalFeeItem(i.AppraisalNumber, i.QuotedPrice, i.EstimatedDays))
                        .ToList()
                    : []))
            .ToList();

        return new GetQuotationValuersResult(query.QuotationId, quotationNumber, appraisals, valuers);
    }

    private sealed record AppraisalRow(string AppraisalNumber, string? PropertyType, string? PropertyLocation);

    private sealed record CompanyRow(
        Guid CompanyQuotationId,
        string CompanyName,
        decimal TotalQuotedPrice,
        int? EstimatedDays);

    private sealed record FeeRow(
        Guid CompanyQuotationId,
        string AppraisalNumber,
        decimal QuotedPrice,
        int? EstimatedDays);
}