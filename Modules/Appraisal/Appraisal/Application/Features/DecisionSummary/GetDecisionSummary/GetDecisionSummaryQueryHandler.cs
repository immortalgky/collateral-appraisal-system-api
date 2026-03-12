using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Data;

namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public class GetDecisionSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IAppraisalDecisionRepository decisionRepository
) : IQueryHandler<GetDecisionSummaryQuery, GetDecisionSummaryResult>
{
    public async Task<GetDecisionSummaryResult> Handle(
        GetDecisionSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var param = new { query.AppraisalId };

        // Query 1: Approach matrix (flat rows to be grouped)
        const string approachSql = """
            SELECT pg.Id AS PropertyGroupId,
                   pg.GroupNumber,
                   paa.ApproachType,
                   paa.ApproachValue,
                   paa.IsSelected,
                   pa.FinalAppraisedValue AS GroupSummaryValue
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa ON pa.PropertyGroupId = pg.Id
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            WHERE pg.AppraisalId = @AppraisalId
            """;

        // Query 2: Building insurance (sum of depreciated price for actual buildings only)
        const string insuranceSql = """
            SELECT ISNULL(SUM(bdd.PriceAfterDepreciation), 0)
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND bdd.IsBuilding = 1
            """;

        // Query 3: Government prices
        const string govPriceSql = """
            SELECT lt.TitleNumber, lt.AreaSquareWa, lt.IsMissingFromSurvey,
                   lt.GovernmentPricePerSqWa, lt.GovernmentPrice
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.PropertyGroupItems gi ON gi.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId = @AppraisalId
            """;

        // Query 4: Approval list
        const string approvalSql = """
            SELECT
                c.CommitteeName,
                ar.Status AS ReviewStatus,
                ar.Id AS ReviewId,
                cm.Id AS CommitteeMemberId,
                cm.MemberName,
                cm.Role,
                cv.Vote,
                CASE
                    WHEN cv.Vote IS NULL THEN 'Pending'
                    WHEN cv.Vote = 'Approve' THEN 'Agree'
                    WHEN cv.Vote = 'Reject' THEN 'Disagree'
                    WHEN cv.Vote = 'RouteBack' THEN 'Route Back'
                    ELSE cv.Vote
                END AS VoteLabel,
                cv.Comments AS Remark,
                cv.VotedAt
            FROM appraisal.AppraisalReviews ar
            JOIN appraisal.Committees c ON c.Id = ar.CommitteeId
            JOIN appraisal.CommitteeMembers cm ON cm.CommitteeId = c.Id AND cm.IsActive = 1
            LEFT JOIN appraisal.CommitteeVotes cv ON cv.ReviewId = ar.Id AND cv.CommitteeMemberId = cm.Id
            WHERE ar.AppraisalId = @AppraisalId
              AND ar.ReviewLevel = 'Committee'
            ORDER BY cm.MemberName
            """;

        // Execute all queries
        var flatRows = (await connectionFactory.QueryAsync<FlatApproachRow>(approachSql, param)).ToList();
        var buildingInsurance = await connectionFactory.ExecuteScalarAsync<decimal>(insuranceSql, param);
        var governmentPrices = (await connectionFactory.QueryAsync<GovernmentPriceRow>(govPriceSql, param)).ToList();
        var approvalRows = (await connectionFactory.QueryAsync<ApprovalRow>(approvalSql, param)).ToList();

        // Stored decision
        var decision = await decisionRepository.GetByAppraisalIdAsync(query.AppraisalId, cancellationToken);

        // Group flat rows into nested approach matrix
        var approachMatrix = flatRows
            .GroupBy(r => new { r.PropertyGroupId, r.GroupNumber, r.GroupSummaryValue })
            .Select(g => new ApproachMatrixGroup(
                g.Key.PropertyGroupId,
                g.Key.GroupNumber,
                g.Key.GroupSummaryValue,
                g.Select(r => new ApproachItem(r.ApproachType, r.ApproachValue, r.IsSelected)).ToList()))
            .OrderBy(g => g.GroupNumber)
            .ToList();

        // Calculate totals
        var totalAppraisalPrice = approachMatrix.Sum(g => g.GroupSummaryValue ?? 0m);
        var forceSellingPrice = totalAppraisalPrice * 0.70m;
        var insurance = buildingInsurance;

        // Government price calculations
        var surveyedTitles = governmentPrices.Where(g => !g.IsMissingFromSurvey).ToList();
        var govTotalArea = surveyedTitles.Sum(g => g.AreaSquareWa ?? 0m);
        var govTotalPrice = surveyedTitles.Sum(g => g.GovernmentPrice ?? 0m);
        var govAvgPerSqWa = govTotalArea > 0 ? govTotalPrice / govTotalArea : 0m;

        // Review calculations
        var totalAppraisalPriceReview = decision?.TotalAppraisalPriceReview;
        var forceSellingPriceReview = totalAppraisalPriceReview.HasValue
            ? totalAppraisalPriceReview.Value * 0.70m
            : (decimal?)null;

        // Build approval list
        string? committeeName = approvalRows.Count > 0 ? approvalRows[0].CommitteeName : null;
        string? reviewStatus = approvalRows.Count > 0 ? approvalRows[0].ReviewStatus : null;
        Guid? reviewId = approvalRows.Count > 0 ? approvalRows[0].ReviewId : null;
        var approvalList = approvalRows.Count > 0
            ? approvalRows.Select(r => new DecisionApprovalListItem(
                r.CommitteeMemberId, r.MemberName, r.Role,
                r.Vote, r.VoteLabel, r.Remark, r.VotedAt)).ToList()
            : null;

        return new GetDecisionSummaryResult(
            ApproachMatrix: approachMatrix,
            TotalAppraisalPrice: totalAppraisalPrice,
            ForceSellingPrice: forceSellingPrice,
            BuildingInsurance: insurance,
            GovernmentPrices: governmentPrices,
            GovernmentPriceTotalArea: govTotalArea,
            GovernmentPriceAvgPerSqWa: govAvgPerSqWa,
            TotalAppraisalPriceReview: totalAppraisalPriceReview,
            ForceSellingPriceReview: forceSellingPriceReview,
            BuildingInsuranceReview: insurance,
            CommitteeName: committeeName,
            ReviewStatus: reviewStatus,
            ReviewId: reviewId,
            ApprovalList: approvalList,
            DecisionId: decision?.Id,
            IsPriceVerified: decision?.IsPriceVerified,
            ConditionType: decision?.ConditionType,
            Condition: decision?.Condition,
            RemarkType: decision?.RemarkType,
            Remark: decision?.Remark,
            AppraiserOpinionType: decision?.AppraiserOpinionType,
            AppraiserOpinion: decision?.AppraiserOpinion,
            CommitteeOpinionType: decision?.CommitteeOpinionType,
            CommitteeOpinion: decision?.CommitteeOpinion,
            AdditionalAssumptions: decision?.AdditionalAssumptions
        );
    }

    private record FlatApproachRow(
        Guid PropertyGroupId,
        int GroupNumber,
        string ApproachType,
        decimal? ApproachValue,
        bool IsSelected,
        decimal? GroupSummaryValue
    );

    private record ApprovalRow(
        string CommitteeName,
        string ReviewStatus,
        Guid ReviewId,
        Guid CommitteeMemberId,
        string MemberName,
        string Role,
        string? Vote,
        string VoteLabel,
        string? Remark,
        DateTime? VotedAt
    );
}
