using Dapper;
using Shared.Data;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalComparables;

public class GetAppraisalComparablesQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalComparablesQuery, GetAppraisalComparablesResult>
{
    public async Task<GetAppraisalComparablesResult> Handle(
        GetAppraisalComparablesQuery query,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        var sql = """
                  SELECT *
                  FROM appraisal.vw_AppraisalComparableList
                  WHERE AppraisalId = @AppraisalId
                  ORDER BY ComparableNumber
                  """;

        var comparables = (await connectionFactory.QueryAsync<AppraisalComparableDto>(sql, parameters)).ToList();

        if (comparables.Count == 0)
            return new GetAppraisalComparablesResult([]);

        // Batch-load adjustments for all comparables
        var comparableIds = comparables.Select(c => c.Id).ToList();

        var adjParams = new DynamicParameters();
        adjParams.Add("ComparableIds", comparableIds);

        var adjSql = """
                     SELECT Id,
                            AppraisalComparableId,
                            AdjustmentCategory,
                            AdjustmentType,
                            AdjustmentPercent,
                            AdjustmentDirection,
                            SubjectValue,
                            ComparableValue,
                            Justification
                     FROM appraisal.ComparableAdjustments
                     WHERE AppraisalComparableId IN @ComparableIds
                     ORDER BY AdjustmentCategory, AdjustmentType
                     """;

        var adjustments = (await connectionFactory.QueryAsync<ComparableAdjustmentDto>(adjSql, adjParams)).ToList();

        var adjustmentsByComparable = adjustments
            .GroupBy(a => a.AppraisalComparableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = comparables.Select(c => c with
        {
            Adjustments = adjustmentsByComparable.GetValueOrDefault(c.Id, [])
        }).ToList();

        return new GetAppraisalComparablesResult(result);
    }
}