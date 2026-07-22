using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Appraisal.Application.Features.DecisionSummary.GetDecisionSummary;

public class GetDecisionSummaryQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IAppraisalDecisionRepository decisionRepository,
    ForceSaleRateResolver forceSaleRateResolver
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
                   InsuranceValue AS BuildingInsuranceReview,
                   ForceSaleRate
            FROM appraisal.ValuationAnalyses
            WHERE AppraisalId = @AppraisalId
            """;

        // Query 3: Government prices
        const string govPriceSql = """
            SELECT lt.TitleNumber, ISNULL(lt.AreaSquareWa, 0) + (ISNULL(lt.AreaNgan, 0) * 100) + (ISNULL(lt.AreaRai, 0) * 400) as AreaSquareWa, lt.IsMissingFromSurvey,
                   lt.GovernmentPricePerSqWa, lt.GovernmentPrice
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            JOIN appraisal.PropertyGroupItems gi ON gi.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId = @AppraisalId
            """;

        // Query 3b: Condo government prices — kept separate from govPriceSql above: condo area is
        // in sq.m. while land area is in Sq.Wa, so mixing rows would corrupt the land AVG.
        const string condoGovPriceSql = """
            SELECT cad.TitleNumber, cad.RoomNumber, cad.UsableArea,
                   cad.GovernmentPricePerSqm, cad.GovernmentPrice
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, CONVERT(char(36), ap.Id)
            """;

        // Query 4: Approval list
        const string approvalSql = """
            SELECT
                c.CommitteeName,
                'Approved' AS ReviewStatus,
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
        var condoGovernmentPrices = (await connectionFactory.QueryAsync<CondoGovernmentPriceQueryRow>(condoGovPriceSql, param))
            .Select(r => new CondoGovernmentPriceRow(r.TitleNumber, r.RoomNumber, r.UsableArea, r.GovernmentPricePerSqm, r.GovernmentPrice))
            .ToList();
        var approvalRows = (await connectionFactory.QueryAsync<ApprovalRow>(approvalSql, param)).ToList();
        var appraisalDate = await connectionFactory.QueryFirstOrDefaultAsync<DateTime?>(appraisalDateSql, param);

        // Stored decision
        var decision = await decisionRepository.GetByAppraisalIdAsync(query.AppraisalId, cancellationToken);

        // Government price calculations — two separate area totals:
        //   surveyedArea — non-missing titles only; drives the Baht/Sq.Wa average
        //                  (missing-from-survey land has no government price, so it must not dilute the avg)
        //   govTotalArea — ALL titles incl. missing-from-survey; shown as the TOTAL Sq.Wa (total land area)
        var surveyedTitles = governmentPrices.Where(g => !g.IsMissingFromSurvey).ToList();
        var surveyedArea = surveyedTitles.Sum(g => g.AreaSquareWa ?? 0m);
        var govTotalPrice = surveyedTitles.Sum(g => g.GovernmentPrice ?? 0m);
        var govTotalArea = governmentPrices.Sum(g => g.AreaSquareWa ?? 0m);
        var govAvgPerSqWa = surveyedArea > 0 ? govTotalPrice / surveyedArea : 0m;

        // Condo government price totals — single area total (sq.m.); no IsMissingFromSurvey
        // equivalent on condo, so every row contributes to both the total and the weighted AVG.
        var condoGovTotalArea = condoGovernmentPrices.Sum(r => r.UsableArea ?? 0m);
        var condoGovTotalPrice = condoGovernmentPrices.Sum(r => r.GovernmentPrice ?? 0m);
        var condoGovAvgPerSqm = condoGovTotalArea > 0 ? condoGovTotalPrice / condoGovTotalArea : 0m;

        // Review values come from ValuationAnalyses
        var totalAppraisalPriceReview = valuationReview?.TotalAppraisalPriceReview;
        var forceSellingPriceReview = valuationReview?.ForceSellingPriceReview;
        var buildingInsuranceReview = valuationReview?.BuildingInsuranceReview;

        // Per-appraisal force-sale rate override, if any. Final resolution (override -> system
        // default, and for block also -> project assumption) happens in the Build*ResultAsync methods.
        var forceSaleRateOverride = valuationReview?.ForceSaleRate;

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
                surveyedArea,
                govAvgPerSqWa,
                condoGovernmentPrices,
                condoGovTotalArea,
                condoGovAvgPerSqm,
                totalAppraisalPriceReview,
                forceSellingPriceReview,
                buildingInsuranceReview,
                forceSaleRateOverride,
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
            surveyedArea,
            govAvgPerSqWa,
            condoGovernmentPrices,
            condoGovTotalArea,
            condoGovAvgPerSqm,
            totalAppraisalPriceReview,
            forceSellingPriceReview,
            buildingInsuranceReview,
            forceSaleRateOverride,
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
        decimal surveyedArea,
        decimal govAvgPerSqWa,
        List<CondoGovernmentPriceRow> condoGovernmentPrices,
        decimal condoGovTotalArea,
        decimal condoGovAvgPerSqm,
        decimal? totalAppraisalPriceReview,
        decimal? forceSellingPriceReview,
        decimal? buildingInsuranceReview,
        decimal? forceSaleRateOverride,
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
            JOIN appraisal.PricingAnalysis pa ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
            WHERE pg.AppraisalId = @AppraisalId
            """;

        var flatRows = (await connectionFactory.QueryAsync<FlatApproachRow>(approachSql, param)).ToList();
        var buildingInsurance = await BuildingInsuranceCalculator.ComputeAsync(connectionFactory, appraisalId);
        var constructionSummary = await BuildConstructionSummaryAsync(connectionFactory, appraisalId);
        var docPresence = await GetConstructionDocPresenceAsync(appraisalId);

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

        var forceSaleRate = await forceSaleRateResolver.ResolveAsync(appraisalId, forceSaleRateOverride, cancellationToken);
        var forceSellingPrice = totalAppraisalPrice * forceSaleRate / 100m;

        return new GetDecisionSummaryResult(
            ApproachMatrix: approachMatrix,
            TotalAppraisalPrice: totalAppraisalPrice,
            ForceSellingPrice: forceSellingPrice,
            ForceSellingRate: forceSaleRate,
            ForceSellingRateOverride: forceSaleRateOverride,
            BuildingInsurance: buildingInsurance,
            GovernmentPrices: governmentPrices,
            GovernmentPriceTotalArea: govTotalArea,
            GovernmentPriceSurveyedArea: surveyedArea,
            GovernmentPriceAvgPerSqWa: govAvgPerSqWa,
            CondoGovernmentPrices: condoGovernmentPrices,
            CondoGovernmentPriceTotalArea: condoGovTotalArea,
            CondoGovernmentPriceAvgPerSqm: condoGovAvgPerSqm,
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
            ExternalAppraiserOpinionType: decision?.ExternalAppraiserOpinionType,
            ExternalAppraiserOpinion: decision?.ExternalAppraiserOpinion,
            CommitteeOpinionType: decision?.CommitteeOpinionType,
            CommitteeOpinion: decision?.CommitteeOpinion,
            InternalAppraiserOpinionType: decision?.InternalAppraiserOpinionType,
            InternalAppraiserOpinion: decision?.InternalAppraiserOpinion,
            AdditionalAssumptions: decision?.AdditionalAssumptions,
            HasConstructionLicenseDoc: decision?.HasConstructionLicenseDoc,
            HasConstructionProgressTableDoc: decision?.HasConstructionProgressTableDoc,
            HasConstructionPhotoDoc: decision?.HasConstructionPhotoDoc,
            ConstructionLicenseDocAttached: docPresence.License,
            ConstructionProgressTableDocAttached: docPresence.ProgressTable,
            ConstructionPhotoDocAttached: docPresence.Photo,
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
        decimal surveyedArea,
        decimal govAvgPerSqWa,
        List<CondoGovernmentPriceRow> condoGovernmentPrices,
        decimal condoGovTotalArea,
        decimal condoGovAvgPerSqm,
        decimal? totalAppraisalPriceReview,
        decimal? forceSellingPriceReview,
        decimal? buildingInsuranceReview,
        decimal? forceSaleRateOverride,
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
            LEFT JOIN appraisal.PricingAnalysis pa ON pa.AnchorId = pm.Id AND pa.SubjectType = 1
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
            -- Exclude sold units from the block appraisal totals (count / appraised value / insurance);
            -- the appraisal covers remaining unsold inventory only, mirroring the Unit Price listing.
            LEFT JOIN appraisal.ProjectUnits pu ON pu.ProjectModelId = pm.Id AND pu.IsSold = 0
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

        var forceSaleRate = await forceSaleRateResolver.ResolveAsync(appraisalId, forceSaleRateOverride, cancellationToken);

        // Build per-model price rows. FSP per model = TotalAppraisalPrice × resolved rate.
        var blockModelPrices = blockModelTotals
            .Select(r => new BlockModelPriceRow(
                r.ProjectModelId,
                r.ModelName,
                r.UnitCount,
                r.TotalAppraisalPrice,
                r.TotalAppraisalPrice * forceSaleRate / 100m,
                r.BuildingInsurance))
            .ToList();

        var totalAppraisalPrice = blockModelPrices.Sum(r => r.TotalAppraisalPrice);
        var forceSellingPrice = blockModelPrices.Sum(r => r.ForceSellingPrice);
        var blockInsurance = blockModelPrices.Sum(r => r.BuildingInsurance);

        return new GetDecisionSummaryResult(
            ApproachMatrix: [],
            TotalAppraisalPrice: totalAppraisalPrice,
            ForceSellingPrice: forceSellingPrice,
            ForceSellingRate: forceSaleRate,
            ForceSellingRateOverride: forceSaleRateOverride,
            BuildingInsurance: blockInsurance,
            GovernmentPrices: governmentPrices,
            GovernmentPriceTotalArea: govTotalArea,
            GovernmentPriceSurveyedArea: surveyedArea,
            GovernmentPriceAvgPerSqWa: govAvgPerSqWa,
            CondoGovernmentPrices: condoGovernmentPrices,
            CondoGovernmentPriceTotalArea: condoGovTotalArea,
            CondoGovernmentPriceAvgPerSqm: condoGovAvgPerSqm,
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
            ExternalAppraiserOpinionType: decision?.ExternalAppraiserOpinionType,
            ExternalAppraiserOpinion: decision?.ExternalAppraiserOpinion,
            CommitteeOpinionType: decision?.CommitteeOpinionType,
            CommitteeOpinion: decision?.CommitteeOpinion,
            InternalAppraiserOpinionType: decision?.InternalAppraiserOpinionType,
            InternalAppraiserOpinion: decision?.InternalAppraiserOpinion,
            AdditionalAssumptions: decision?.AdditionalAssumptions,
            // Construction docs are not applicable to block appraisals — return the stored override
            // (usually null) with no auto-derived presence.
            HasConstructionLicenseDoc: decision?.HasConstructionLicenseDoc,
            HasConstructionProgressTableDoc: decision?.HasConstructionProgressTableDoc,
            HasConstructionPhotoDoc: decision?.HasConstructionPhotoDoc,
            ConstructionLicenseDocAttached: false,
            ConstructionProgressTableDocAttached: false,
            ConstructionPhotoDocAttached: false,
            IsBlock: true,
            BlockApproachMatrix: blockApproachMatrix,
            BlockModelPrices: blockModelPrices,
            ConstructionSummary: null,
            AppraisalDate: appraisalDate
        );
    }

    // Auto-derived presence of the three "เอกสารประกอบ" documents that feed the construction summary
    // report. Mirrors AppraisalSummaryConstructionDataProvider so the Decision screen pre-fills each
    // checkbox with the same value the report would render (before any manual override):
    //   License      = D026 (สำเนาใบอนุญาตปลูกสร้าง) attached at request stage (request.RequestDocuments)
    //   ProgressTable = D012 attached on the Documents page OR the CI summary-mode document slot
    //   Photo        = D011 (ภาพถ่ายการก่อสร้าง) attached on the Documents page
    private async Task<ConstructionDocPresence> GetConstructionDocPresenceAsync(Guid appraisalId)
    {
        const string sql = """
            SELECT
                CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM appraisal.Appraisals a
                    JOIN request.RequestDocuments rd ON rd.RequestId = a.RequestId
                    WHERE a.Id = @AppraisalId
                      AND rd.DocumentType = 'D026'
                      AND (rd.DocumentId IS NOT NULL OR rd.FilePath IS NOT NULL)
                ) THEN 1 ELSE 0 END AS bit) AS License,
                CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM appraisal.AppraisalDocuments ad
                    WHERE ad.AppraisalId = @AppraisalId AND ad.DocumentTypeCode = 'D012'
                      AND (ad.DocumentId IS NOT NULL OR ad.FileName IS NOT NULL)
                ) OR EXISTS (
                    SELECT 1 FROM appraisal.ConstructionInspections ci
                    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
                    WHERE ap.AppraisalId = @AppraisalId AND ci.FileName IS NOT NULL
                ) THEN 1 ELSE 0 END AS bit) AS ProgressTable,
                CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM appraisal.AppraisalDocuments ad
                    WHERE ad.AppraisalId = @AppraisalId AND ad.DocumentTypeCode = 'D011'
                      AND (ad.DocumentId IS NOT NULL OR ad.FileName IS NOT NULL)
                ) THEN 1 ELSE 0 END AS bit) AS Photo;
            """;

        var row = await connectionFactory.QueryFirstOrDefaultAsync<ConstructionDocPresence>(
            sql, new { AppraisalId = appraisalId });
        return row ?? ConstructionDocPresence.Empty;
    }

    private sealed record ConstructionDocPresence(bool License, bool ProgressTable, bool Photo)
    {
        public static ConstructionDocPresence Empty { get; } = new(false, false, false);
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
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId AND pa.SubjectType = 0
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
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

        // รายละเอียดรายอาคาร — same IsFullDetail CASE as ciAggregateSql above, but per property
        // instead of aggregated. ConstructionInspection is 1:1 with AppraisalProperty.
        const string ciDetailSql = """
            SELECT
                ap.Id                             AS AppraisalPropertyId,
                NULLIF(LTRIM(RTRIM(bad.HouseNumber)), '')      AS HouseNumber,
                NULLIF(LTRIM(RTRIM(bad.BuiltOnTitleNumber)), '') AS TitleNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(bad.ModelName)), ''),
                         NULLIF(LTRIM(RTRIM(bad.PropertyName)), '')) AS ModelName,
                ci.TotalValue                     AS TotalValue,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ISNULL(ci.SummaryPreviousValue, 0)
                     ELSE ISNULL(wd_agg.PreviousPropertyValueSum, 0)
                END AS PreviousValue,
                CASE WHEN ci.IsFullDetail = 0
                     THEN ISNULL(ci.SummaryCurrentValue, 0)
                     ELSE ISNULL(wd_agg.CurrentPropertyValueSum, 0)
                END AS CurrentValue
            FROM appraisal.ConstructionInspections ci
            JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
            LEFT JOIN appraisal.BuildingAppraisalDetails bad ON bad.AppraisalPropertyId = ap.Id
            LEFT JOIN (
                SELECT ConstructionInspectionId,
                       SUM(PreviousPropertyValue) AS PreviousPropertyValueSum,
                       SUM(CurrentPropertyValue)  AS CurrentPropertyValueSum
                FROM appraisal.ConstructionWorkDetails
                GROUP BY ConstructionInspectionId
            ) wd_agg ON wd_agg.ConstructionInspectionId = ci.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, CONVERT(char(36), ap.Id)
            """;

        // อาคารที่สร้างเสร็จ 100% ก่อนการตรวจงวดงาน — same NOT EXISTS predicate as
        // nonCiBuildingValueSql, grouped per property instead of summed flat, so the rows
        // reconcile with the "Building Value Pre-inspection" column of the milestone table.
        const string completedBuildingSql = """
            SELECT
                ap.Id                             AS AppraisalPropertyId,
                NULLIF(LTRIM(RTRIM(bad.HouseNumber)), '')      AS HouseNumber,
                NULLIF(LTRIM(RTRIM(bad.BuiltOnTitleNumber)), '') AS TitleNumber,
                COALESCE(NULLIF(LTRIM(RTRIM(bad.ModelName)), ''),
                         NULLIF(LTRIM(RTRIM(bad.PropertyName)), '')) AS ModelName,
                ISNULL(SUM(bdd.PriceAfterDepreciation), 0) AS AppraisalValue
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND NOT EXISTS (
                  SELECT 1 FROM appraisal.ConstructionInspections ci
                  WHERE ci.AppraisalPropertyId = ap.Id
              )
            GROUP BY ap.Id, ap.SequenceNumber, bad.HouseNumber, bad.BuiltOnTitleNumber,
                     bad.ModelName, bad.PropertyName
            ORDER BY ap.SequenceNumber, CONVERT(char(36), ap.Id)
            """;

        // ชื่ออาคาร = village name of the first L/LB land property (same source as report RS04)
        const string villageSql = """
            SELECT TOP 1 lad.Village
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND ap.PropertyType IN ('L', 'LB')
            ORDER BY ap.SequenceNumber
            """;

        var landValue = await connectionFactory.QueryFirstOrDefaultAsync<decimal>(landValueSql, p);
        var nonCiBuilding = await connectionFactory.QueryFirstOrDefaultAsync<decimal>(nonCiBuildingValueSql, p);
        var ci = await connectionFactory.QueryFirstOrDefaultAsync<CiAggregateRow>(ciAggregateSql, p);

        if (ci is null || ci.CITotalValue == 0m)
            return null;

        var village = await connectionFactory.QueryFirstOrDefaultAsync<string?>(villageSql, p);
        var detailRows = await connectionFactory.QueryAsync<CiDetailRow>(ciDetailSql, p);
        var completedRows = await connectionFactory.QueryAsync<CompletedBuildingRow>(completedBuildingSql, p);

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

        // % derived from value ratios, consistent with the milestone rows above — deliberately
        // NOT from SummaryCurrentProgressPct / SUM(CurrentProportionPct), so the detail rows
        // reconcile against the card they sit under.
        var buildings = detailRows
            .Select(d => new ConstructionBuildingRow(
                d.AppraisalPropertyId,
                d.HouseNumber,
                d.TitleNumber,
                d.ModelName,
                d.TotalValue,
                d.PreviousValue,
                d.CurrentValue,
                d.TotalValue > 0 ? d.PreviousValue / d.TotalValue * 100m : 0m,
                d.TotalValue > 0 ? d.CurrentValue / d.TotalValue * 100m : 0m))
            .ToList();

        var completedBuildings = completedRows
            .Select(c => new ConstructionCompletedBuildingRow(
                c.AppraisalPropertyId,
                c.HouseNumber,
                c.TitleNumber,
                c.ModelName,
                c.AppraisalValue))
            .ToList();

        return new ConstructionSummaryData(village, rows, buildings, completedBuildings);
    }

    // Mapped by NAME (settable properties), not by constructor position — the three adjacent
    // string? columns would corrupt silently if a positional record were used and the SELECT
    // column order ever changed.
    private sealed class CiDetailRow
    {
        public Guid AppraisalPropertyId { get; init; }
        public string? HouseNumber { get; init; }
        public string? TitleNumber { get; init; }
        public string? ModelName { get; init; }
        public decimal TotalValue { get; init; }
        public decimal PreviousValue { get; init; }
        public decimal CurrentValue { get; init; }
    }

    private sealed class CompletedBuildingRow
    {
        public Guid AppraisalPropertyId { get; init; }
        public string? HouseNumber { get; init; }
        public string? TitleNumber { get; init; }
        public string? ModelName { get; init; }
        public decimal AppraisalValue { get; init; }
    }

    // Mapped by NAME (settable properties) — TitleNumber/RoomNumber are adjacent string? columns,
    // same positional-mapping risk documented on CiDetailRow above.
    private sealed class CondoGovernmentPriceQueryRow
    {
        public string? TitleNumber { get; init; }
        public string? RoomNumber { get; init; }
        public decimal? UsableArea { get; init; }
        public decimal? GovernmentPricePerSqm { get; init; }
        public decimal? GovernmentPrice { get; init; }
    }

    private record CiAggregateRow(
        decimal CITotalValue,
        decimal CIPreviousValue,
        decimal CICurrentValue
    );

    private record ValuationReviewRow(
        decimal? TotalAppraisalPriceReview,
        decimal? ForceSellingPriceReview,
        decimal? BuildingInsuranceReview,
        decimal? ForceSaleRate
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
