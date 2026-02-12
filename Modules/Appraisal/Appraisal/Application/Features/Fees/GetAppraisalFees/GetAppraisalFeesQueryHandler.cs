using Dapper;

namespace Appraisal.Application.Features.Fees.GetAppraisalFees;

public class GetAppraisalFeesQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAppraisalFeesQuery, GetAppraisalFeesResult>
{
    public async Task<GetAppraisalFeesResult> Handle(
        GetAppraisalFeesQuery query,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        // Get fee summaries
        const string feeSql = """
            SELECT * FROM appraisal.vw_AppraisalFeeList
            WHERE AppraisalId = @AppraisalId
            ORDER BY CreatedOn DESC
            """;

        var fees = (await connectionFactory.QueryAsync<AppraisalFeeDto>(feeSql, parameters)).ToList();

        if (fees.Count == 0)
            return new GetAppraisalFeesResult([]);

        // Get fee items for all fees of this appraisal
        var feeIds = fees.Select(f => f.Id).ToList();

        const string itemsSql = """
            SELECT
                i.Id,
                i.AppraisalFeeId,
                i.FeeCode,
                i.FeeDescription,
                i.FeeAmount,
                i.RequiresApproval,
                i.ApprovalStatus,
                i.ApprovedBy,
                i.ApprovedAt,
                i.RejectionReason
            FROM appraisal.AppraisalFeeItems i
            WHERE i.AppraisalFeeId IN @FeeIds
            ORDER BY i.FeeCode
            """;

        var itemParams = new DynamicParameters();
        itemParams.Add("FeeIds", feeIds);

        var items = (await connectionFactory.QueryAsync<AppraisalFeeItemDto>(itemsSql, itemParams)).ToList();

        // Group items by fee
        var itemsByFee = items.GroupBy(i => i.AppraisalFeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = fees.Select(f => f with
        {
            Items = itemsByFee.GetValueOrDefault(f.Id, [])
        }).ToList();

        return new GetAppraisalFeesResult(result);
    }
}
