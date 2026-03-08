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

        // Query 1: Approach matrix
        const string approachSql = """
            SELECT pg.Id AS PropertyGroupId, pg.GroupNumber,
                   paa.ApproachType,
                   pfv.FinalValue, pfv.FinalValueRounded,
                   pa.FinalAppraisedValue AS GroupSummaryValue
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa ON pa.PropertyGroupId = pg.Id
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            JOIN appraisal.PricingAnalysisMethods pam ON pam.ApproachId = paa.Id AND pam.IsSelected = 1
            LEFT JOIN appraisal.PricingFinalValues pfv ON pfv.PricingMethodId = pam.Id
            WHERE pg.AppraisalId = @AppraisalId
            """;

        // Query 2: Building insurance
        const string insuranceSql = """
            SELECT ISNULL(SUM(bad.BuildingInsurancePrice), 0)
            FROM appraisal.BuildingAppraisalDetails bad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            """;

        // Query 3: Government prices
        const string govPriceSql = """
            SELECT lt.TitleNumber, lt.AreaSquareWa, lt.IsMissingFromSurvey,
                   lt.GovernmentPricePerSqWa, lt.GovernmentPrice
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
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
        var approachMatrix = (await connectionFactory.QueryAsync<ApproachMatrixRow>(approachSql, param)).ToList();
        var buildingInsurance = await connectionFactory.ExecuteScalarAsync<decimal>(insuranceSql, param);
        var governmentPrices = (await connectionFactory.QueryAsync<GovernmentPriceRow>(govPriceSql, param)).ToList();
        var approvalRows = (await connectionFactory.QueryAsync<ApprovalRow>(approvalSql, param)).ToList();

        // Query 4: Stored decision
        var decision = await decisionRepository.GetByAppraisalIdAsync(query.AppraisalId, cancellationToken);

        // Calculate totals
        var totalAppraisalPrice = approachMatrix
            .Where(r => r.GroupSummaryValue.HasValue)
            .Select(r => r.GroupSummaryValue!.Value)
            .Distinct() // Avoid double-counting same group with multiple approaches
            .Sum();

        // Use distinct PropertyGroupId to avoid double-counting
        var groupSummaryValues = approachMatrix
            .GroupBy(r => r.PropertyGroupId)
            .Select(g => g.First().GroupSummaryValue ?? 0m)
            .ToList();
        totalAppraisalPrice = groupSummaryValues.Sum();

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
