using Dapper;

namespace Appraisal.Application.Evaluations.Queries;

public record GetEvaluationListQuery(
    PaginationRequest PaginationRequest,
    string? AppraisalNumber  = null,
    string? CustomerName     = null,
    string? AppraisalStatus  = null,
    string? AppraiserName    = null,
    string? EvaluationStatus = null
) : IQuery<GetEvaluationListResult>;

public record GetEvaluationListResult(PaginatedResult<AppraisalEvaluationListItem> Items);

public record AppraisalEvaluationListItem(
    Guid      AppraisalId,
    string?   AppraisalNumber,
    string?   CustomerName,
    DateTime? ReportReceivedDate,
    string?   AppraisalStatus,
    string?   ExternalAppraiserName,
    string?   AssigneeCompanyId,
    decimal?  AppraisalValue,
    Guid?     EvaluationId,
    string    EvaluationStatus,
    decimal?  TotalScore);

public class GetEvaluationListQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetEvaluationListQuery, GetEvaluationListResult>
{
    private const string BaseQuery =
        "SELECT AppraisalId, AppraisalNumber, CustomerName, ReportReceivedDate, " +
        "AppraisalStatus, ExternalAppraiserName, AssigneeCompanyId, AppraisalValue, " +
        "EvaluationId, EvaluationStatus, TotalScore " +
        "FROM appraisal.vw_AppraisalEvaluationList " +
        "WHERE 1 = 1";

    public async Task<GetEvaluationListResult> Handle(
        GetEvaluationListQuery query,
        CancellationToken cancellationToken)
    {
        var sql = BaseQuery;
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.AppraisalNumber))
        {
            sql += " AND AppraisalNumber LIKE @AppraisalNumber";
            parameters.Add("AppraisalNumber", "%" + query.AppraisalNumber.Trim() + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            sql += " AND CustomerName LIKE @CustomerName";
            parameters.Add("CustomerName", "%" + query.CustomerName.Trim() + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.AppraisalStatus))
        {
            sql += " AND AppraisalStatus = @AppraisalStatus";
            parameters.Add("AppraisalStatus", query.AppraisalStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.AppraiserName))
        {
            sql += " AND ExternalAppraiserName LIKE @AppraiserName";
            parameters.Add("AppraiserName", "%" + query.AppraiserName.Trim() + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.EvaluationStatus))
        {
            sql += " AND EvaluationStatus = @EvaluationStatus";
            parameters.Add("EvaluationStatus", query.EvaluationStatus.Trim());
        }

        var result = await connectionFactory.QueryPaginatedAsync<AppraisalEvaluationListItem>(
            sql,
            "AppraisalId DESC",
            query.PaginationRequest,
            parameters);

        return new GetEvaluationListResult(result);
    }
}
