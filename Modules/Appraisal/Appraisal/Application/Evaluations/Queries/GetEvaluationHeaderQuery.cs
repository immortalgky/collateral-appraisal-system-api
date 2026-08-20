using Dapper;

namespace Appraisal.Application.Evaluations.Queries;

public record GetEvaluationHeaderQuery(Guid AppraisalId) : IQuery<AppraisalEvaluationHeader?>;

public record AppraisalEvaluationHeader(
    Guid      AppraisalId,
    string?   AppraisalNumber,
    string?   AppraisalStatus,
    string?   BankingSegment,
    string?   CustomerName,
    DateTime? ReportReceivedDate,
    string?   AppraiserCompanyName,
    // Thai name (null when absent); the client picks by its own locale. Position is load-bearing —
    // it must stay directly after AppraiserCompanyName to match the SELECT's column order.
    string?   AppraiserCompanyNameLocal,
    string?   AssigneeCompanyId,
    string?   InternalAppraiserId,
    string?   InternalAppraiserName,
    string?   CollateralTypes,
    string?   InspectionDates,
    string?   DepartmentOfAppraisal);

public class GetEvaluationHeaderQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetEvaluationHeaderQuery, AppraisalEvaluationHeader?>
{
    public async Task<AppraisalEvaluationHeader?> Handle(
        GetEvaluationHeaderQuery query,
        CancellationToken cancellationToken)
    {
        const string sql =
            "SELECT AppraisalId, AppraisalNumber, AppraisalStatus, BankingSegment, CustomerName, " +
            "ReportReceivedDate, AppraiserCompanyName, AppraiserCompanyNameLocal, AssigneeCompanyId, " +
            "InternalAppraiserId, InternalAppraiserName, " +
            "CollateralTypes, InspectionDates, DepartmentOfAppraisal " +
            "FROM appraisal.vw_AppraisalEvaluationHeader " +
            "WHERE AppraisalId = @AppraisalId";

        var connection = connectionFactory.GetOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<AppraisalEvaluationHeader>(
            sql,
            new { query.AppraisalId });
    }
}
