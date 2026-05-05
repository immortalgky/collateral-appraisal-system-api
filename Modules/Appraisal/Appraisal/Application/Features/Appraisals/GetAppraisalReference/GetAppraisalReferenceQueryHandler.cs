using Appraisal.Contracts.Appraisals;
using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalReference;

/// <summary>
/// Returns the minimum reference data (number, value, completed date) for a given appraisal.
/// Used cross-module by the Request query to populate PrevAppraisalNumber/Value/Date at read time.
/// Returns null (does not throw) when the appraisal does not exist.
/// </summary>
public class GetAppraisalReferenceQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IRequestHandler<GetAppraisalReferenceQuery, AppraisalReferenceResult?>
{
    public async Task<AppraisalReferenceResult?> Handle(
        GetAppraisalReferenceQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        const string sql = """
            SELECT AppraisalNumber, AppraisalValue, CompletedDate
            FROM appraisal.vw_AppraisalCopyTemplate
            WHERE AppraisalId = @AppraisalId
            """;

        var row = await connection.QueryFirstOrDefaultAsync<AppraisalReferenceRow>(sql, parameters);

        if (row is null)
            return null;

        return new AppraisalReferenceResult(row.AppraisalNumber, row.AppraisalValue, row.CompletedDate);
    }

    private class AppraisalReferenceRow
    {
        public string? AppraisalNumber { get; set; }
        public decimal? AppraisalValue { get; set; }
        public DateTime? CompletedDate { get; set; }
    }
}
