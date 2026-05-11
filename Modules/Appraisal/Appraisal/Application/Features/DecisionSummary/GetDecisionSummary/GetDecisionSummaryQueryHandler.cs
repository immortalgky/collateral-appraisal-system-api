using Appraisal.Domain.Appraisals;
using Dapper;
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

        // Probe: is this a block appraisal?
        const string projectProbeSql = """
            SELECT TOP 1 Id FROM appraisal.Projects WHERE AppraisalId = @AppraisalId
            """;

        var projectId = await connectionFactory.QueryFirstOrDefaultAsync<Guid?>(projectProbeSql, param);
        var isBlock = projectId.HasValue;

        // Query 2b: Review values overridden / populated in ValuationAnalyses
        const string valuationReviewSql = """
            SELECT AppraisedValue AS TotalAppraisalPriceReview,
                   ForcedSaleValue AS ForceSellingPriceReview,
                   InsuranceValue AS BuildingInsuranceReview
            FROM appraisal.ValuationAnalyses
            WHERE AppraisalId = @AppraisalId
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

        // Query 5: Appraisal date — most recent non-cancelled appointment for this appraisal
        const string appraisalDateSql = """
            SELECT TOP 1 ap.AppointmentDateTime
            FROM appraisal.Appointments ap
            JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
            WHERE aa.AppraisalId = @AppraisalId
              AND ap.Status <> 'Cancelled'
            ORDER BY ap.AppointmentDateTime DESC
            """;

        var valuationReview = await connectionFactory.QueryFirstOrDefaultAsync<ValuationReviewRow>(valuationReviewSql, param);
        var governmentPrices = (await connectionFactory.QueryAsync<GovernmentPriceRow>(govPriceSql, param)).ToList();
        var approvalRows = (await connectionFactory.QueryAsync<ApprovalRow>(approvalSql, param)).ToList();
        var appraisalDate = await connectionFactory.QueryFirstOrDefaultAsync<DateTime?>(appraisalDateSql, param);

        // Stored decision
        var decision = await decisionRepository.GetByAppraisalIdAsync(query.AppraisalId, cancellationToken);

        // Government price calculations
        var surveyedTitles = governmentPrices.Where(g => !g.IsMissingFromSurvey).ToList();
        var govTotalArea = surveyedTitles.Sum(g => g.AreaSquareWa ?? 0m);
        var govTotalPrice = surveyedTitles.Sum(g => g.GovernmentPrice ?? 0m);
        var govAvgPerSqWa = govTotalArea > 0 ? govTotalPrice / govTotalArea : 0m;

        // Review values come from ValuationAnalyses
        var totalAppraisalPriceReview = valuationReview?.TotalAppraisalPriceReview;
        var forceSellingPriceReview = valuationReview?.ForceSellingPriceReview;
        var buildingInsuranceReview = valuationReview?.BuildingInsuranceReview;

        // Build approval list
        string? committeeName = approvalRows.Count > 0 ? approvalRows[0].CommitteeName : null;
        string? reviewStatus = approvalRows.Count > 0 ? approvalRows[0].ReviewStatus : null;
        Guid? reviewId = approvalRows.Count > 0 ? approvalRows[0].ReviewId : null;
        var approvalList = approvalRows.Count > 0
            ? approvalRows.Select(r => new DecisionApprovalListItem(
                r.CommitteeMemberId, r.MemberName, r.Role,
                r.Vote, r.VoteLabel, r.Remark, r.VotedAt)).ToList()
            : null;

        if (isBlock)
        {
            return await BuildBlockResultAsync(
                query.AppraisalId,
                governmentPrices,
                govTotalArea,
                govAvgPerSqWa,
                totalAppraisalPriceReview,
                forceSellingPriceReview,
                buildingInsuranceReview,
                committeeName,
                reviewStatus,
                reviewId,
                approvalList,
                decision,
                appraisalDate,
                cancellationToken);
        }

        return await BuildNormalResultAsync(
            query.AppraisalId,
            param,
            governmentPrices,
            govTotalArea,
            govAvgPerSqWa,
            totalAppraisalPriceReview,
            forceSellingPriceReview,
            buildingInsuranceReview,
            committeeName,
            reviewStatus,
            reviewId,
            approvalList,
            decision,
            appraisalDate,
            cancellationToken);
    }

    private async Task<GetDecisionSummaryResult> BuildNormalResultAsync(
        Guid appraisalId,
        object param,
        List<GovernmentPriceRow> governmentPrices,
        decimal govTotalArea,
        decimal govAvgPerSqWa,
        decimal? totalAppraisalPriceReview,
        decimal? forceSellingPriceReview,
        decimal? buildingInsuranceReview,
        string? committeeName,
        string? reviewStatus,
        Guid? reviewId,
        List<DecisionApprovalListItem>? approvalList,
        AppraisalDecision? decision,
        DateTime? appraisalDate,
        CancellationToken cancellationToken)
    {
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

        var flatRows = (await connectionFactory.QueryAsync<FlatApproachRow>(approachSql, param)).ToList();
        var buildingInsurance = await BuildingInsuranceCalculator.ComputeAsync(connectionFactory, appraisalId);
        var constructionSummary = await BuildConstructionSummaryAsync(connectionFactory, appraisalId);

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

        var totalAppraisalPrice = approachMatrix.Sum(g => g.GroupSummaryValue ?? 0m);
        var forceSellingPrice = totalAppraisalPrice * 0.70m;

        return new GetDecisionSummaryResult(
            ApproachMatrix: approachMatrix,
            TotalAppraisalPrice: totalAppraisalPrice,
            ForceSellingPrice: forceSellingPrice,
            BuildingInsurance: buildingInsurance,
            GovernmentPrices: governmentPrices,
            GovernmentPriceTotalArea: govTotalArea,
            GovernmentPriceAvgPerSqWa: govAvgPerSqWa,
            TotalAppraisalPriceReview: totalAppraisalPriceReview,
            ForceSellingPriceReview: forceSellingPriceReview,
            BuildingInsuranceReview: buildingInsuranceReview,
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
            AdditionalAssumptions: decision?.AdditionalAssumptions,
            IsBlock: false,
            BlockApproachMatrix: null,
            BlockModelPrices: null,
            ConstructionSummary: constructionSummary,
            AppraisalDate: appraisalDate
        );
    }

    private async Task<GetDecisionSummaryResult> BuildBlockResultAsync(
        Guid appraisalId,
        List<GovernmentPriceRow> governmentPrices,
        decimal govTotalArea,
        decimal govAvgPerSqWa,
        decimal? totalAppraisalPriceReview,
        decimal? forceSellingPriceReview,
        decimal? buildingInsuranceReview,
        string? committeeName,
        string? reviewStatus,
        Guid? reviewId,
        List<DecisionApprovalListItem>? approvalList,
        AppraisalDecision? decision,
        DateTime? appraisalDate,
        CancellationToken cancellationToken)
    {
        var blockParam = new DynamicParameters();
        blockParam.Add("AppraisalId", appraisalId);

        // Block query 1: flat approach rows per model (one row per model × approach).
        // LEFT JOINs so models without a PricingAnalysis (in-progress) still appear with all-null approach cells —
        // keeps row counts in sync with BlockModelPrices (which also LEFT JOINs ProjectUnits/Prices).
        const string blockApproachSql = """
            SELECT pm.Id AS ProjectModelId,
                   pm.ModelName,
                   paa.ApproachType,
                   paa.ApproachValue,
                   paa.IsSelected
            FROM appraisal.ProjectModels pm
            JOIN appraisal.Projects p ON p.Id = pm.ProjectId
            LEFT JOIN appraisal.PricingAnalysis pa ON pa.ProjectModelId = pm.Id
            LEFT JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            WHERE p.AppraisalId = @AppraisalId
            ORDER BY pm.Id, paa.ApproachType
            """;

        // Block query 2: per-model unit counts, total appraised prices, and building insurance
        const string blockModelTotalsSql = """
            SELECT pm.Id AS ProjectModelId,
                   pm.ModelName,
                   COUNT(pu.Id) AS UnitCount,
                   ISNULL(SUM(pup.TotalAppraisalValueRounded), 0) AS TotalAppraisalPrice,
                   ISNULL(SUM(pup.CoverageAmount), 0) AS BuildingInsurance
            FROM appraisal.ProjectModels pm
            JOIN appraisal.Projects p ON p.Id = pm.ProjectId
            LEFT JOIN appraisal.ProjectUnits pu ON pu.ProjectModelId = pm.Id
            LEFT JOIN appraisal.ProjectUnitPrices pup ON pup.ProjectUnitId = pu.Id
            WHERE p.AppraisalId = @AppraisalId
            GROUP BY pm.Id, pm.ModelName
            ORDER BY pm.Id
            """;

        var blockApproachRows = (await connectionFactory.QueryAsync<FlatBlockApproachRow>(blockApproachSql, blockParam)).ToList();
        var blockModelTotals = (await connectionFactory.QueryAsync<BlockModelTotalRow>(blockModelTotalsSql, blockParam)).ToList();

        // Build a lookup: ProjectModelId → TotalAppraisalPrice
        var modelTotalLookup = blockModelTotals.ToDictionary(r => r.ProjectModelId, r => r.TotalAppraisalPrice);

        // Pivot approach rows into one BlockApproachMatrixRow per model
        var blockApproachMatrix = blockApproachRows
            .GroupBy(r => new { r.ProjectModelId, r.ModelName })
            .Select(g =>
            {
                var approaches = g.ToList();
                decimal? marketValue = approaches.FirstOrDefault(a => a.ApproachType == "Market")?.ApproachValue;
                decimal? costValue = approaches.FirstOrDefault(a => a.ApproachType == "Cost")?.ApproachValue;
                decimal? incomeValue = approaches.FirstOrDefault(a => a.ApproachType == "Income")?.ApproachValue;
                decimal? residualValue = approaches.FirstOrDefault(a => a.ApproachType == "Residual")?.ApproachValue;
                string? selectedApproach = approaches.FirstOrDefault(a => a.IsSelected == true)?.ApproachType;
                var modelTotal = modelTotalLookup.GetValueOrDefault(g.Key.ProjectModelId, 0m);

                return new BlockApproachMatrixRow(
                    g.Key.ProjectModelId,
                    g.Key.ModelName,
                    marketValue,
                    costValue,
                    incomeValue,
                    residualValue,
                    selectedApproach,
                    modelTotal);
            })
            .ToList();

        // Build per-model price rows. FSP per model = TotalAppraisalPrice × 0.70 (matches project-level rule).
        var blockModelPrices = blockModelTotals
            .Select(r => new BlockModelPriceRow(
                r.ProjectModelId,
                r.ModelName,
                r.UnitCount,
                r.TotalAppraisalPrice,
                r.TotalAppraisalPrice * 0.70m,
                r.BuildingInsurance))
            .ToList();

        var totalAppraisalPrice = blockModelPrices.Sum(r => r.TotalAppraisalPrice);
        var forceSellingPrice = blockModelPrices.Sum(r => r.ForceSellingPrice);
        var blockInsurance = blockModelPrices.Sum(r => r.BuildingInsurance);

        return new GetDecisionSummaryResult(
            ApproachMatrix: [],
            TotalAppraisalPrice: totalAppraisalPrice,
            ForceSellingPrice: forceSellingPrice,
            BuildingInsurance: blockInsurance,
            GovernmentPrices: governmentPrices,
            GovernmentPriceTotalArea: govTotalArea,
            GovernmentPriceAvgPerSqWa: govAvgPerSqWa,
            TotalAppraisalPriceReview: totalAppraisalPriceReview,
            ForceSellingPriceReview: forceSellingPriceReview,
            BuildingInsuranceReview: buildingInsuranceReview,
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
            AdditionalAssumptions: decision?.AdditionalAssumptions,
            IsBlock: true,
            BlockApproachMatrix: blockApproachMatrix,
            BlockModelPrices: blockModelPrices,
            ConstructionSummary: null,
            AppraisalDate: appraisalDate
        );
    }

    private static async Task<ConstructionSummaryData?> BuildConstructionSummaryAsync(
        ISqlConnectionFactory connectionFactory,
        Guid appraisalId)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        const string landValueSql = """
            SELECT ISNULL(SUM(pfv.LandValue), 0)
            FROM appraisal.PricingFinalValues pfv
            JOIN appraisal.PricingAnalysisMethods pam ON pam.Id = pfv.PricingMethodId
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.PropertyGroupId
            WHERE pg.AppraisalId = @AppraisalId
            """;

        const string nonCiBuildingValueSql = """
            SELECT ISNULL(SUM(bdd.PriceAfterDepreciation), 0)
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND NOT EXISTS (
                  SELECT 1 FROM appraisal.ConstructionInspections ci
                  WHERE ci.AppraisalPropertyId = ap.Id
              )
            """;

        const string ciAggregateSql = """
            SELECT
                ISNULL(SUM(ci.TotalValue), 0) AS CITotalValue,
                ISNULL(SUM(
                    CASE WHEN ci.IsFullDetail = 0
                         THEN ISNULL(ci.SummaryPreviousValue, 0)
                         ELSE ISNULL(wd_agg.PreviousPropertyValueSum, 0)
                    END
                ), 0) AS CIPreviousValue,
                ISNULL(SUM(
                    CASE WHEN ci.IsFullDetail = 0
                         THEN ISNULL(ci.SummaryCurrentValue, 0)
                         ELSE ISNULL(wd_agg.CurrentPropertyValueSum, 0)
                    END
                ), 0) AS CICurrentValue
            FROM appraisal.ConstructionInspections ci
            JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
            LEFT JOIN (
                SELECT ConstructionInspectionId,
                       SUM(PreviousPropertyValue) AS PreviousPropertyValueSum,
                       SUM(CurrentPropertyValue)  AS CurrentPropertyValueSum
                FROM appraisal.ConstructionWorkDetails
                GROUP BY ConstructionInspectionId
            ) wd_agg ON wd_agg.ConstructionInspectionId = ci.Id
            WHERE ap.AppraisalId = @AppraisalId
            """;

        var landValue = await connectionFactory.QueryFirstOrDefaultAsync<decimal>(landValueSql, p);
        var nonCiBuilding = await connectionFactory.QueryFirstOrDefaultAsync<decimal>(nonCiBuildingValueSql, p);
        var ci = await connectionFactory.QueryFirstOrDefaultAsync<CiAggregateRow>(ciAggregateSql, p);

        if (ci is null || ci.CITotalValue == 0m)
            return null;

        var ciTotal = ci.CITotalValue;
        var ciPrev = ci.CIPreviousValue;
        var ciCurrent = ci.CICurrentValue;

        var prevPct = ciTotal > 0 ? ciPrev / ciTotal * 100m : 0m;
        var currentPct = ciTotal > 0 ? ciCurrent / ciTotal * 100m : 0m;
        var increasedPct = currentPct - prevPct;
        var remainingPct = 100m - currentPct;

        var rows = new List<ConstructionSummaryRow>
        {
            new("Previous",
                prevPct,
                landValue + nonCiBuilding + ciPrev,
                landValue,
                nonCiBuilding + ciPrev,
                ciPrev),
            new("Construction Increased",
                increasedPct,
                ciCurrent - ciPrev,
                0m,
                ciCurrent - ciPrev,
                ciCurrent - ciPrev),
            new("Current",
                currentPct,
                landValue + nonCiBuilding + ciCurrent,
                landValue,
                nonCiBuilding + ciCurrent,
                ciCurrent),
            new("Remaining construction",
                remainingPct,
                ciTotal - ciCurrent,
                0m,
                ciTotal - ciCurrent,
                ciTotal - ciCurrent),
            new("Complete ( 100% )",
                100m,
                landValue + nonCiBuilding + ciTotal,
                landValue,
                nonCiBuilding + ciTotal,
                ciTotal),
        };

        return new ConstructionSummaryData(rows);
    }

    private record CiAggregateRow(
        decimal CITotalValue,
        decimal CIPreviousValue,
        decimal CICurrentValue
    );

    private record ValuationReviewRow(
        decimal? TotalAppraisalPriceReview,
        decimal? ForceSellingPriceReview,
        decimal? BuildingInsuranceReview
    );

    private record FlatApproachRow(
        Guid PropertyGroupId,
        int GroupNumber,
        string ApproachType,
        decimal? ApproachValue,
        bool IsSelected,
        decimal? GroupSummaryValue
    );

    private record FlatBlockApproachRow(
        Guid ProjectModelId,
        string? ModelName,
        string? ApproachType,
        decimal? ApproachValue,
        bool? IsSelected
    );

    private record BlockModelTotalRow(
        Guid ProjectModelId,
        string? ModelName,
        int UnitCount,
        decimal TotalAppraisalPrice,
        decimal BuildingInsurance
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
