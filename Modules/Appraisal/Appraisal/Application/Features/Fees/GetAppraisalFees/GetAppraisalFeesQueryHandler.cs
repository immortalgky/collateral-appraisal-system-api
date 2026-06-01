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
        var feeSql = """
                     SELECT * 
                     FROM appraisal.vw_AppraisalFeeList
                     WHERE AppraisalId = @AppraisalId
                     ORDER BY CreatedAt DESC
                     """;

        var fees = (await connectionFactory.QueryAsync<AppraisalFeeDto>(feeSql, parameters)).ToList();

        if (fees.Count == 0)
            return new GetAppraisalFeesResult([]);

        // Drives FE visibility of the Construction Inspection Fee input.
        // Building properties have type code 'B' (Building) or 'LB' (Land+Building) — both have a
        // BuildingAppraisalDetail row keyed by AppraisalPropertyId; IsUnderConstruction lives there.
        const string ucSql = """
                             SELECT CASE WHEN EXISTS (
                                 SELECT 1
                                 FROM appraisal.BuildingAppraisalDetails bad
                                 INNER JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
                                 WHERE ap.AppraisalId = @AppraisalId
                                   AND bad.IsUnderConstruction = 1
                             ) THEN 1 ELSE 0 END
                             """;
        var hasBuildingUnderConstruction =
            await connectionFactory.ExecuteScalarAsync<bool>(ucSql, parameters);

        // Stamp the flag onto every fee row so the FE can read it directly off the loaded fee.
        fees = fees
            .Select(f => f with { HasBuildingUnderConstruction = hasBuildingUnderConstruction })
            .ToList();

        // Get fee items for all fees of this appraisal
        var feeIds = fees.Select(f => f.Id).ToList();

        var itemsSql = """
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

        var paymentSql = """
                         SELECT
                            h.Id,
                            h.AppraisalFeeId,
                            h.PaymentDate,
                            h.PaymentAmount,
                            h.Source,
                            h.CreatedAt
                         FROM appraisal.AppraisalFeePaymentHistory h
                         WHERE AppraisalFeeId IN @FeeIds
                         ORDER BY PaymentDate, CreatedAt
                         """;

        var paymentParams = new DynamicParameters();
        paymentParams.Add("FeeIds", feeIds);

        var paymentHistory =
            (await connectionFactory.QueryAsync<PaymentHistoryDto>(paymentSql, paymentParams)).ToList();

        var paymentHistoryByFee = paymentHistory.GroupBy(h => h.AppraisalFeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = fees.Select(f => f with
        {
            Items = itemsByFee.GetValueOrDefault(f.Id, []),
            PaymentHistory = paymentHistoryByFee.GetValueOrDefault(f.Id, [])
        }).ToList();

        return new GetAppraisalFeesResult(result);
    }
}