using Shared.Data;

namespace Appraisal.Application.Features.DecisionSummary;

internal static class BuildingInsuranceCalculator
{
    internal static Task<decimal> ComputeAsync(ISqlConnectionFactory connectionFactory, Guid appraisalId)
    {
        const string sql = """
            SELECT ISNULL(SUM(bdd.PriceAfterDepreciation), 0)
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND bdd.IsBuilding = 1
            """;

        return connectionFactory.QueryFirstOrDefaultAsync<decimal>(sql, new { AppraisalId = appraisalId });
    }
}
