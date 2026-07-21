using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;
using Shared.Configuration;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.3.1
/// "ใบสรุปรายงานการประเมิน – ที่ดินและสิ่งปลูกสร้าง".
///
/// Phase C — QueryMultiple batch:
///   Batch 1 (15 result sets, single round-trip, all off @AppraisalId):
///     RS01  Q1  appraisal.Appraisals — header
///     RS02  Q2  request.RequestCustomers — customer names (RequestId subquery)
///     RS03  Q3  appraisal.Appointments — inspection date
///     RS04  Q4  appraisal.LandAppraisalDetails — first land detail
///     RS05  Q5  appraisal.AppraisalAssignments — assignment
///     RS06  Q8  appraisal.ValuationAnalyses — totals
///     RS07  Q9  appraisal.PropertyGroups + GroupValuations — per-group rows
///     RS08  Q10 appraisal.PricingAnalysisMethods — method flags
///     RS09  Q11 appraisal.AppraisalDecisions — decision
///     RS10  Q12 appraisal.AppraisalReviews + Meetings — review
///     RS11  Q13 request.Requests — requestor (RequestId subquery)
///     RS12  Q14 workflow.CompletedTasks — checker/verifier (RequestId subquery)
///     RS13  Q15 appraisal.PropertyGroupItems + LandTitles — per-group titles
///     RS14  Q16 appraisal.PropertyGroupItems + BuildingAppraisalDetails — per-group buildings
///     RS15  CollateralType code→Thai map
///
///   Conditionally (Batch 2 — 0–2 extra round-trips):
///     Q6  auth.AspNetUsers — staff (only for Internal assignment)
///     Q7  auth.Companies — company (only for External assignment with valid Guid)
///     Q12b appraisal.CommitteeVotes — votes (only when review row exists)
///     AO dept — auth.AspNetUsers (only when RequestorName blank or department needed)
/// </summary>
public sealed class AppraisalSummaryLandBuildingDataProvider(
    ISqlConnectionFactory connectionFactory,
    ISystemConfigurationReader configReader,
    ILogger<AppraisalSummaryLandBuildingDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-land-building";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var forceSaleRateDefault = await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", 70m, cancellationToken);
        var model = await BuildAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);

        logger.LogDebug(
            "AppraisalSummaryLandBuilding model assembled for appraisal {AppraisalId}: " +
            "{GroupCount} groups, {ApproverCount} approvers, showMeeting={ShowMeeting}",
            appraisalId, model.Groups.Count, model.Approvers.Count, model.ShowMeeting);

        return model;
    }

    /// <summary>
    /// Builds an <see cref="AppraisalSummaryModel"/> from an open connection — the standard
    /// (land/building/condo/machine) summary header. Called by <see cref="GetModelAsync"/>
    /// (standalone form) and by <c>AppraisalBookDataProvider</c> (unified internal book, "standard" body).
    /// </summary>
    internal static async Task<AppraisalSummaryModel> BuildAsync(
        System.Data.IDbConnection connection,
        Guid appraisalId,
        decimal forceSaleRateDefault,
        CancellationToken cancellationToken)
    {
        // ── Batch 1: 15 result sets, single round-trip ───────────────────────────
        const string batchSql = """
            -- RS01: Q1 — Appraisal header
            SELECT
                a.AppraisalNumber,
                a.RequestId,
                a.AppraisalType,
                a.Status           AS AppraisalStatus,
                a.Purpose          AS PurposeCode,
                COALESCE(pPurpose.[description], a.Purpose) AS AppraisalPurpose,
                a.FacilityLimit
            FROM appraisal.Appraisals a
            LEFT JOIN parameter.Parameters pPurpose
                ON pPurpose.[group]    = 'AppraisalPurpose'
               AND pPurpose.[language] = 'TH'
               AND pPurpose.[isactive] = 1
               AND pPurpose.[code]     = a.Purpose
            WHERE a.Id = @AppraisalId
              AND a.IsDeleted = 0;

            -- RS02: Q2 — Customer names (RequestId via subquery)
            SELECT rc.Name
            FROM request.RequestCustomers rc
            WHERE rc.RequestId = (
                SELECT a2.RequestId
                FROM appraisal.Appraisals a2
                WHERE a2.Id = @AppraisalId AND a2.IsDeleted = 0)
            ORDER BY rc.Name;

            -- RS03: Q3 — Appraisal date (latest non-cancelled appointment)
            SELECT TOP 1 ap.AppointmentDateTime
            FROM appraisal.Appointments ap
            JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
            WHERE aa.AppraisalId = @AppraisalId
              AND ap.Status <> 'Cancelled'
            ORDER BY ap.AppointmentDateTime DESC;

            -- RS04: Q4 — Land detail (first property). Geocodes + codes resolved to Thai.
            SELECT TOP 1
                lad.OwnerName,
                COALESCE(pUrban.[description], lad.UrbanPlanningType) AS UrbanPlanningType,
                lad.ObligationDetails,
                lad.Village,
                lad.Soi,
                lad.Street                AS Road,
                lad.LandUseType,
                lad.LandUseTypeOther,
                lad.LandEntranceExitType,
                lad.LandEntranceExitTypeOther,
                COALESCE(tsub.NameTh,  lad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, lad.District)    AS District,
                COALESCE(tprov.NameTh, lad.Province)    AS Province,
                COALESCE(pLandOffice.[description], lad.LandOffice) AS LandOffice,
                lad.Latitude,
                lad.Longitude,
                (SELECT TOP 1 pgi.PropertyGroupId
                 FROM appraisal.PropertyGroupItems pgi
                 WHERE pgi.AppraisalPropertyId = ap.Id) AS LandGroupId,
                (SELECT AVG(lt.GovernmentPrice)
                 FROM appraisal.LandTitles lt
                 JOIN appraisal.LandAppraisalDetails lad2 ON lad2.Id = lt.LandAppraisalDetailId
                 JOIN appraisal.AppraisalProperties ap2 ON ap2.Id = lad2.AppraisalPropertyId
                 WHERE ap2.AppraisalId = @AppraisalId
                   AND ISNULL(lt.IsMissingFromSurvey, 0) = 0
                   AND lt.GovernmentPrice IS NOT NULL) AS GovernmentPrice
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = lad.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = lad.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = lad.SubDistrict
            LEFT JOIN parameter.Parameters pUrban
                ON pUrban.[group]    = 'TypeOfUrbanPlanning'
               AND pUrban.[language] = 'TH'
               AND pUrban.[isactive] = 1
               AND pUrban.[code]     = lad.UrbanPlanningType
            LEFT JOIN parameter.Parameters pLandOffice
                ON pLandOffice.[group]    = 'LandOffice'
               AND pLandOffice.[language] = 'TH'
               AND pLandOffice.[isactive] = 1
               AND pLandOffice.[code]     = lad.LandOffice
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS05: Q5 — Assignment (latest active)
            SELECT TOP 1
                aa.AssignmentType,
                aa.AssigneeCompanyId,
                aa.InternalAppraiserId
            FROM appraisal.AppraisalAssignments aa
            WHERE aa.AppraisalId = @AppraisalId
              AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
            ORDER BY aa.AssignedAt DESC, aa.Id DESC;

            -- RS06: Q8 — Valuation totals
            SELECT
                va.AppraisedValue,
                va.ForcedSaleValue,
                va.InsuranceValue,
                va.ForceSaleRate
            FROM appraisal.ValuationAnalyses va
            WHERE va.AppraisalId = @AppraisalId;

            -- RS07: Q9 — Per-group valuation rows.
            -- Per-group values come from the selected PricingAnalysis → Approach → Method →
            -- PricingFinalValue (GroupValuations is not populated by the live flow).
            SELECT
                pg.Id                AS GroupId,
                pg.GroupNumber,
                pg.GroupName,
                pa.FinalAppraisedValue AS GroupAppraisalValue,
                pfv.ApproachType,
                pfv.LandValue,
                pfv.BuildingValue,
                pfv.FinalValueAdjusted AS LandUnitPrice,
                pfv.AppraisalPrice,
                pfv.FinalValueRounded,
                pfv.ValuePerUnit,
                pfv.UnitType,
                pfv.IncludeLandArea,
                (SELECT TOP 1 ap.PropertyType
                 FROM appraisal.PropertyGroupItems gi2
                 JOIN appraisal.AppraisalProperties ap ON ap.Id = gi2.AppraisalPropertyId
                 WHERE gi2.PropertyGroupId = pg.Id
                 ORDER BY gi2.SequenceInGroup) AS PropertyType,
                (SELECT TOP 1 lt.TitleNumber
                 FROM appraisal.PropertyGroupItems gi3
                 JOIN appraisal.AppraisalProperties ap3 ON ap3.Id = gi3.AppraisalPropertyId
                 JOIN appraisal.LandAppraisalDetails lad3 ON lad3.AppraisalPropertyId = ap3.Id
                 JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad3.Id
                 WHERE gi3.PropertyGroupId = pg.Id
                 ORDER BY gi3.SequenceInGroup, lt.Id) AS FirstTitleNumber,
                (SELECT TOP 1 lt2.AreaRai
                 FROM appraisal.PropertyGroupItems gi4
                 JOIN appraisal.AppraisalProperties ap4 ON ap4.Id = gi4.AppraisalPropertyId
                 JOIN appraisal.LandAppraisalDetails lad4 ON lad4.AppraisalPropertyId = ap4.Id
                 JOIN appraisal.LandTitles lt2 ON lt2.LandAppraisalDetailId = lad4.Id
                 WHERE gi4.PropertyGroupId = pg.Id
                 ORDER BY gi4.SequenceInGroup, lt2.Id) AS AreaRai,
                (SELECT TOP 1 lt3.AreaNgan
                 FROM appraisal.PropertyGroupItems gi5
                 JOIN appraisal.AppraisalProperties ap5 ON ap5.Id = gi5.AppraisalPropertyId
                 JOIN appraisal.LandAppraisalDetails lad5 ON lad5.AppraisalPropertyId = ap5.Id
                 JOIN appraisal.LandTitles lt3 ON lt3.LandAppraisalDetailId = lad5.Id
                 WHERE gi5.PropertyGroupId = pg.Id
                 ORDER BY gi5.SequenceInGroup, lt3.Id) AS AreaNgan,
                (SELECT TOP 1 lt4.AreaSquareWa
                 FROM appraisal.PropertyGroupItems gi6
                 JOIN appraisal.AppraisalProperties ap6 ON ap6.Id = gi6.AppraisalPropertyId
                 JOIN appraisal.LandAppraisalDetails lad6 ON lad6.AppraisalPropertyId = ap6.Id
                 JOIN appraisal.LandTitles lt4 ON lt4.LandAppraisalDetailId = lad6.Id
                 WHERE gi6.PropertyGroupId = pg.Id
                 ORDER BY gi6.SequenceInGroup, lt4.Id) AS AreaSquareWa
            FROM appraisal.PropertyGroups pg
            LEFT JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
            OUTER APPLY (
                SELECT TOP 1
                    pap.ApproachType,
                    fv.LandValue,
                    fv.BuildingValue,
                    fv.FinalValueAdjusted,
                    fv.AppraisalPrice,
                    fv.FinalValueRounded,
                    fv.IncludeLandArea,
                    pm.ValuePerUnit,
                    pm.UnitType
                FROM appraisal.PricingAnalysisApproaches pap
                JOIN appraisal.PricingAnalysisMethods pm
                    ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                JOIN appraisal.PricingFinalValues fv
                    ON fv.PricingMethodId = pm.Id
                WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
            ) pfv
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber;

            -- RS08: Q10 — Selected pricing APPROACH per group (วิธีการประเมิน, scoped to shown groups)
            SELECT DISTINCT pg.Id AS GroupId, paa.ApproachType AS MethodType
            FROM appraisal.PricingAnalysisApproaches paa
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId
              AND pa.SubjectType = 0
              AND paa.IsSelected = 1;

            -- RS09: Q11 — Appraisal decision
            SELECT
                ad.AppraiserOpinion,
                ad.CommitteeOpinion,
                ad.Condition,
                ad.Remark
            FROM appraisal.AppraisalDecisions ad
            WHERE ad.AppraisalId = @AppraisalId;

            -- RS10: Q12 — Review + meeting
            SELECT
                ar.Id             AS ReviewId,
                ar.MeetingId,
                m.MeetingNo,
                m.StartAt         AS MeetingDate
            FROM appraisal.AppraisalReviews ar
            LEFT JOIN workflow.Meetings m ON m.Id = ar.MeetingId
            WHERE ar.AppraisalId = @AppraisalId;

            -- RS11: Q13 — Requestor (RequestId via subquery)
            SELECT r.Requestor, r.RequestorName
            FROM request.Requests r
            WHERE r.Id = (
                SELECT a3.RequestId
                FROM appraisal.Appraisals a3
                WHERE a3.Id = @AppraisalId AND a3.IsDeleted = 0);

            -- RS12: Q14 — Checker/verifier completed tasks (RequestId via subquery)
            SELECT
                ct.ActivityId,
                u.FirstName + ' ' + u.LastName AS FullName,
                u.Position,
                ct.CompletedAt
            FROM workflow.CompletedTasks ct
            LEFT JOIN auth.AspNetUsers u ON u.UserName = ct.AssignedTo
            WHERE ct.CorrelationId = (
                SELECT a4.RequestId
                FROM appraisal.Appraisals a4
                WHERE a4.Id = @AppraisalId AND a4.IsDeleted = 0)
              AND ct.ActivityId IN (
                  'int-appraisal-execution',
                  'int-appraisal-check',
                  'int-appraisal-verification',
                  'appraisal-book-verification')
            ORDER BY ct.CompletedAt;

            -- RS13: Q15 — Per-group land titles (all groups)
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                lt.Id         AS TitleId,
                lt.TitleNumber,
                lt.BookNumber,
                lt.PageNumber,
                lt.LandParcelNumber,
                lt.SurveyNumber,
                lt.MapSheetNumber,
                lt.AreaRai,
                lt.AreaNgan,
                lt.AreaSquareWa,
                COALESCE(tsub.NameTh,  lad.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, lad.District)    AS District,
                COALESCE(tprov.NameTh, lad.Province)    AS Province
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
            JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = lad.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = lad.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = lad.SubDistrict
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup, lt.Id;

            -- RS14: Q16 — Per-group building details (all groups)
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                bad.Id               AS BuildingId,
                bad.PropertyName,
                bad.OwnerName,
                bad.BuildingType,
                bad.BuildingTypeOther,
                CASE WHEN bad.BuildingType = '99' AND NULLIF(bad.BuildingTypeOther, '') IS NOT NULL
                     THEN bad.BuildingTypeOther
                     ELSE COALESCE(ptBT.Description, bad.BuildingTypeOther, bad.BuildingType) END AS BuildingTypeDisplay,
                bad.NumberOfFloors,
                bad.HouseNumber,
                bad.TotalBuildingArea,
                bad.BuildingAge,
                bad.BuildingConditionType,
                bad.BuildingConditionTypeOther,
                CASE WHEN bad.BuildingConditionType = '99' AND NULLIF(bad.BuildingConditionTypeOther, '') IS NOT NULL
                     THEN bad.BuildingConditionTypeOther
                     ELSE COALESCE(ptBC.Description, bad.BuildingConditionTypeOther, bad.BuildingConditionType) END AS BuildingConditionDisplay
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.AppraisalPropertyId = ap.Id
            LEFT JOIN parameter.Parameters ptBT
                ON ptBT.[Group] = 'BuildingType'
               AND ptBT.[Language] = 'TH'
               AND ptBT.[Code] = bad.BuildingType
               AND ptBT.IsActive = 1
            LEFT JOIN parameter.Parameters ptBC
                ON ptBC.[Group] = 'BuildingCondition'
               AND ptBC.[Language] = 'TH'
               AND ptBC.[Code] = bad.BuildingConditionType
               AND ptBC.IsActive = 1
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup;

            -- RS15: CollateralType code→Thai map
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'CollateralType' AND [Language] = 'TH' AND IsActive = 1;

            -- RS16: LandEntranceExit code→Thai map (entry/exit rights)
            SELECT [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [group] = 'LandEntranceExit' AND [language] = 'TH' AND [isactive] = 1;

            -- RS17: LandUse code→Thai map (utilization)
            SELECT [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [group] = 'LandUse' AND [language] = 'TH' AND [isactive] = 1;

            -- RS18: Per-group depreciation items (cost-approach building + ส่วนพัฒนา breakdown)
            SELECT
                pgi.PropertyGroupId,
                bdd.BuildingAppraisalDetailId,
                bdd.IsBuilding,
                bdd.AreaDescription,
                bdd.Area,
                bdd.[Year],
                bdd.PriceAfterDepreciation
            FROM appraisal.BuildingDepreciationDetails bdd
            JOIN appraisal.BuildingAppraisalDetails bad ON bad.Id = bdd.BuildingAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
            JOIN appraisal.PropertyGroups pg ON pg.Id = pgi.PropertyGroupId
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, bdd.IsBuilding DESC, bdd.Id;

            -- RS19: Per-group landfill (สภาพที่ดิน) + land use (การใช้ประโยชน์) from first land property
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                CASE WHEN lad.LandFillType = '99' AND NULLIF(lad.LandFillTypeOther, '') IS NOT NULL
                     THEN lad.LandFillTypeOther
                     ELSE COALESCE(pFill.[description], lad.LandFillTypeOther, lad.LandFillType) END AS LandFill,
                lad.LandUseType,
                lad.LandUseTypeOther
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
            JOIN appraisal.PropertyGroups pg ON pg.Id = pgi.PropertyGroupId
            LEFT JOIN parameter.Parameters pFill
                ON pFill.[group] = 'Landfill' AND pFill.[language] = 'TH'
               AND pFill.[isactive] = 1 AND pFill.[code] = lad.LandFillType
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup;

            -- RS20: Collateral address + loan limits from the Request detail (geocodes resolved)
            SELECT TOP 1
                rd.HouseNumber,
                rd.ProjectName,
                rd.Moo,
                rd.Soi,
                rd.Road,
                COALESCE(tsub.NameTh,  rd.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, rd.District)    AS District,
                COALESCE(tprov.NameTh, rd.Province)    AS Province,
                rd.FacilityLimit,
                rd.AdditionalFacilityLimit,
                rd.PreviousFacilityLimit
            FROM request.RequestDetails rd
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = rd.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = rd.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = rd.SubDistrict
            WHERE rd.RequestId = (
                SELECT a5.RequestId FROM appraisal.Appraisals a5
                WHERE a5.Id = @AppraisalId AND a5.IsDeleted = 0);

            -- RS21: Distinct request property types
            SELECT DISTINCT rp.PropertyType
            FROM request.RequestProperties rp
            WHERE rp.RequestId = (
                SELECT a6.RequestId FROM appraisal.Appraisals a6
                WHERE a6.Id = @AppraisalId AND a6.IsDeleted = 0)
              AND rp.PropertyType IS NOT NULL;

            -- RS22: Land titles for the government price (per sq.wa), excluding missing-from-survey
            SELECT
                lt.TitleNumber,
                lt.GovernmentPricePerSqWa
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND ISNULL(lt.IsMissingFromSurvey, 0) = 0
              AND lt.GovernmentPricePerSqWa IS NOT NULL
            ORDER BY lt.GovernmentPricePerSqWa, lt.Id;

            -- RS23: ราคาประเมินเดิม — the prior-appraisal link plus its LIVE appraised value,
            -- resolved through appraisal.Appraisals.PrevAppraisalId. Same field used for the current
            -- appraisal in RS06. PrevAppraisalId is returned alongside the value so callers can
            -- distinguish "no prior appraisal" (hide the field) from "prior appraisal exists but has
            -- no valuation yet" (show the field with a dash). LEFT JOIN keeps one row either way.
            SELECT a7.PrevAppraisalId,
                   va.AppraisedValue AS PrevAppraisedValue
            FROM appraisal.Appraisals a7
            LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a7.PrevAppraisalId
            WHERE a7.Id = @AppraisalId AND a7.IsDeleted = 0;
            """;

        var batchParams = new DynamicParameters();
        batchParams.Add("AppraisalId", appraisalId);

        HeaderRow? header;
        List<string> customerNames;
        DateTime? appraisalDate;
        LandRow? land;
        AssignmentRow? assignment;
        ValuationRow? valuation;
        List<GroupRow> groupRows;
        List<MethodTypeRow> methodRows;
        DecisionRow? decision;
        ReviewRow? review;
        RequestorRow? requestorRow;
        List<CompletedTaskRow> completedTaskRows;
        List<GroupTitleRow> groupTitleRows;
        List<GroupBuildingRow> groupBuildingRows;
        List<ParamRow> collateralTypeParams;
        List<ParamRow> entranceExitParams;
        List<ParamRow> landUseParams;
        List<GroupDepreciationRow> depreciationRows;
        List<GroupLandFillRow> landFillRows;
        RequestAddressRow? requestAddress;
        List<string> requestPropertyTypes;
        List<GovPriceRow> govPriceRows;
        AppraisalSummaryCommonLoader.PrevAppraisalRow? prevAppraisal;

        using (var multi = await connection.QueryMultipleAsync(batchSql, batchParams))
        {
            // RS01
            header = await multi.ReadFirstOrDefaultAsync<HeaderRow>();
            if (header is null)
                throw new NotFoundException("Appraisal", appraisalId.ToString());

            // RS02
            customerNames = (await multi.ReadAsync<string>()).ToList();

            // RS03
            appraisalDate = await multi.ReadFirstOrDefaultAsync<DateTime?>();

            // RS04
            land = await multi.ReadFirstOrDefaultAsync<LandRow>();

            // RS05
            assignment = await multi.ReadFirstOrDefaultAsync<AssignmentRow>();

            // RS06
            valuation = await multi.ReadFirstOrDefaultAsync<ValuationRow>();

            // RS07 — this report shows only land / building / land+building groups
            // (condo, machine, vehicle, vessel groups belong to their own summary reports).
            var landBuildingFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "L", "B", "LB", "LSL", "LSB", "LS" };
            groupRows = (await multi.ReadAsync<GroupRow>())
                .Where(g => g.PropertyType != null && landBuildingFamily.Contains(g.PropertyType))
                .ToList();

            // RS08
            methodRows = (await multi.ReadAsync<MethodTypeRow>()).ToList();

            // RS09
            decision = await multi.ReadFirstOrDefaultAsync<DecisionRow>();

            // RS10
            review = await multi.ReadFirstOrDefaultAsync<ReviewRow>();

            // RS11
            requestorRow = await multi.ReadFirstOrDefaultAsync<RequestorRow>();

            // RS12
            completedTaskRows = (await multi.ReadAsync<CompletedTaskRow>()).ToList();

            // RS13
            groupTitleRows = (await multi.ReadAsync<GroupTitleRow>()).ToList();

            // RS14
            groupBuildingRows = (await multi.ReadAsync<GroupBuildingRow>()).ToList();

            // RS15
            collateralTypeParams = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS16
            entranceExitParams = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS17
            landUseParams = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS18
            depreciationRows = (await multi.ReadAsync<GroupDepreciationRow>()).ToList();

            // RS19
            landFillRows = (await multi.ReadAsync<GroupLandFillRow>()).ToList();

            // RS20
            requestAddress = await multi.ReadFirstOrDefaultAsync<RequestAddressRow>();

            // RS21
            requestPropertyTypes = (await multi.ReadAsync<string>()).ToList();

            // RS22
            govPriceRows = (await multi.ReadAsync<GovPriceRow>()).ToList();

            // RS23
            prevAppraisal = await multi.ReadFirstOrDefaultAsync<AppraisalSummaryCommonLoader.PrevAppraisalRow>();
        }

        var customerName = customerNames.Count > 0
            ? string.Join(" และ ", customerNames)
            : null;

        // Collateral address (ที่ตั้งทรัพย์สิน) comes from the Request detail; geocodes already
        // resolved to Thai in RS20. Format: เลขที่ {House No} {ProjectName} หมู่ {Moo} ซอย {Soi}
        // ถนน {Road} ตำบล/แขวง {SubDistrict} อำเภอ/เขต {District} จังหวัด {Province}.
        var collateralAddress = requestAddress is not null
            ? ThaiAddressFormatter.FormatLandBuilding(
                houseNumber: requestAddress.HouseNumber,
                village: requestAddress.ProjectName,
                moo: requestAddress.Moo,
                soi: requestAddress.Soi,
                road: requestAddress.Road,
                subDistrict: requestAddress.SubDistrict,
                district: requestAddress.District,
                province: requestAddress.Province)
            : null;

        var gps = ThaiAddressFormatter.FormatGps(land?.Latitude, land?.Longitude);

        // ── Conditional Q6/Q7: Appraiser (header) resolution ──────────────────────
        // The sign-off ผู้ประเมิน name is resolved later from the workflow completed task
        // (int-appraisal-execution for internal, appraisal-book-verification for external).
        bool isInternal = assignment is not null && string.Equals(
            assignment.AssignmentType, "Internal", StringComparison.OrdinalIgnoreCase);

        string? appraiser = null;

        if (assignment is not null)
        {
            if (isInternal)
            {
                appraiser = "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";
            }
            else if (!string.IsNullOrWhiteSpace(assignment.AssigneeCompanyId)
                     && Guid.TryParse(assignment.AssigneeCompanyId, out var companyGuid))
            {
                const string companySql = """
                    SELECT c.Name FROM auth.Companies c WHERE c.Id = @CompanyId
                    """;
                var companyParams = new DynamicParameters();
                companyParams.Add("CompanyId", companyGuid);
                appraiser = await connection.QueryFirstOrDefaultAsync<string>(companySql, companyParams);
            }
        }

        // FSD field 11 must always show an appraiser. When the assignment is missing or an
        // external company could not be resolved, fall back to the internal bank name.
        if (string.IsNullOrWhiteSpace(appraiser))
            appraiser = "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";

        // ── Committee / sub-committee votes from workflow.ApprovalVotes (by AppraisalId) ──
        const string votesSql = """
            SELECT
                COALESCE(NULLIF(LTRIM(RTRIM(u.FirstName + ' ' + u.LastName)), ''), av.Member) AS MemberName,
                COALESCE(NULLIF(u.Position, ''), av.MemberRole) AS Position,
                av.Vote,
                av.Comments   AS Comment,
                av.Member     AS Member,
                av.VotedAt    AS VotedAt
            FROM workflow.ApprovalVotes av
            LEFT JOIN auth.AspNetUsers u ON u.UserName = av.Member
            WHERE av.AppraisalId = @AppraisalId
            ORDER BY av.VotedAt
            """;
        var voteParams = new DynamicParameters();
        voteParams.Add("AppraisalId", appraisalId);

        // One row per member (latest vote), in case of re-approval rounds.
        var votes = (await connection.QueryAsync<VoteRow>(votesSql, voteParams))
            .GroupBy(v => v.Member, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(v => v.VotedAt).First())
            .ToList();

        List<ApproverRow> approvers = votes.Select(v => new ApproverRow
        {
            Name = v.MemberName,
            Position = v.Position,
            Comment = v.Comment,
            Approved = string.Equals(v.Vote, "Approve", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(v.Vote, "Reject", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : (bool?)null
        }).ToList();

        int approveCount = votes.Count(v => string.Equals(v.Vote, "Approve", StringComparison.OrdinalIgnoreCase));
        int rejectCount = votes.Count(v => string.Equals(v.Vote, "Reject", StringComparison.OrdinalIgnoreCase));
        bool? approverDecisionApproved = votes.Count > 0 ? approveCount > rejectCount : null;

        // Approval date (latest vote) — used for the sub-committee header (no meeting).
        DateTime? approvalDate = votes.Count > 0 ? votes.Max(v => v.VotedAt) : (DateTime?)null;
        // The committee block shows only for completed appraisals (approval info is final then).
        bool isCompleted = string.Equals(header.AppraisalStatus, "Completed", StringComparison.OrdinalIgnoreCase);

        // ── Conditional AO resolution ─────────────────────────────────────────────
        string? aoName = null;
        if (requestorRow is not null)
        {
            string? displayName = requestorRow.RequestorName;

            if (string.IsNullOrWhiteSpace(displayName)
                && !string.IsNullOrWhiteSpace(requestorRow.Requestor))
            {
                const string aoUserSql = """
                    SELECT u.FirstName + ' ' + u.LastName AS FullName,
                           u.Department
                    FROM auth.AspNetUsers u
                    WHERE u.UserName = @UserCode
                    """;
                var aoUserParams = new DynamicParameters();
                aoUserParams.Add("UserCode", requestorRow.Requestor);
                var aoUser = await connection.QueryFirstOrDefaultAsync<AoUserRow>(aoUserSql, aoUserParams);
                displayName = aoUser?.FullName?.Trim();

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    aoName = string.IsNullOrWhiteSpace(aoUser?.Department)
                        ? displayName
                        : $"{displayName} - {aoUser.Department}";
                }
            }
            else if (!string.IsNullOrWhiteSpace(displayName))
            {
                if (!string.IsNullOrWhiteSpace(requestorRow.Requestor))
                {
                    const string aoDeptSql = """
                        SELECT u.Department
                        FROM auth.AspNetUsers u
                        WHERE u.UserName = @UserCode
                        """;
                    var aoDeptParams = new DynamicParameters();
                    aoDeptParams.Add("UserCode", requestorRow.Requestor);
                    var dept = await connection.QueryFirstOrDefaultAsync<string?>(aoDeptSql, aoDeptParams);
                    aoName = string.IsNullOrWhiteSpace(dept)
                        ? displayName
                        : $"{displayName} - {dept}";
                }
                else
                {
                    aoName = displayName;
                }
            }
        }

        // ── Q14 post-processing ──────────────────────────────────────────────────
        var latestByActivity = completedTaskRows
            .Where(r => r.ActivityId is not null)
            .GroupBy(r => r.ActivityId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.CompletedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        // ผู้ประเมิน (sign-off appraiser): internal = int-appraisal-execution actor,
        // external = appraisal-book-verification actor.
        CompletedTaskRow? staffTask;
        if (isInternal)
            latestByActivity.TryGetValue("int-appraisal-execution", out staffTask);
        else
            latestByActivity.TryGetValue("appraisal-book-verification", out staffTask);

        // ผู้ตรวจสอบ / ผู้สอบทาน come only from their own activities; they stay blank
        // until int-appraisal-check / int-appraisal-verification are actually completed.
        latestByActivity.TryGetValue("int-appraisal-check", out var checkerTask);
        latestByActivity.TryGetValue("int-appraisal-verification", out var verifyTask);

        string? staffName = string.IsNullOrWhiteSpace(staffTask?.FullName) ? null : staffTask.FullName!.Trim();
        string? staffPosition = string.IsNullOrWhiteSpace(staffTask?.Position) ? null : staffTask.Position;
        string? checkerName = string.IsNullOrWhiteSpace(checkerTask?.FullName) ? null : checkerTask.FullName!.Trim();
        string? checkerPosition = string.IsNullOrWhiteSpace(checkerTask?.Position) ? null : checkerTask.Position;
        string? verifyName = string.IsNullOrWhiteSpace(verifyTask?.FullName) ? null : verifyTask.FullName!.Trim();
        string? verifyPosition = string.IsNullOrWhiteSpace(verifyTask?.Position) ? null : verifyTask.Position;

        // ── Build group lookups ───────────────────────────────────────────────────
        var titlesByGroup = groupTitleRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SequenceInGroup).ThenBy(r => r.TitleId).ToList());

        var buildingsByGroup = groupBuildingRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SequenceInGroup).ToList());

        var depreciationByGroup = depreciationRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var collateralTypeMap = collateralTypeParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(grp => grp.Key, grp => grp.First().Description, StringComparer.OrdinalIgnoreCase);

        var entranceExitMap = ToParamMap(entranceExitParams);
        var landUseMap = ToParamMap(landUseParams);

        // PropertyType stores domain family codes, so map family → CollateralType code before lookup.
        string? TranslateCollateralType(string? code) =>
            CollateralFamilyTranslator.ToThai(code, collateralTypeMap);

        // RS07 gives us only the FIRST member by SequenceInGroup, so a mixed Land+Building group
        // mislabels itself as สิ่งปลูกสร้าง whenever the building happens to sort first. Derive the
        // family from what the group actually contains instead. Leasehold families (LS*) pass
        // through — they carry their own CollateralType codes (LS→09, LSL→29, LSB→05) that the
        // plain L/B/LB triple cannot express.
        static string? DeriveFamily(string? stored, bool hasLand, bool hasBuilding)
        {
            if (stored is not null && stored.StartsWith("LS", StringComparison.OrdinalIgnoreCase))
                return stored;

            return (hasLand, hasBuilding) switch
            {
                (true, true) => "LB",
                (true, false) => "L",
                (false, true) => "B",
                _ => stored,   // no composition signal — keep whatever RS07 gave us
            };
        }

        // ── Build per-group summary rows ──────────────────────────────────────────
        var summaryGroups = groupRows.Select(g =>
        {
            titlesByGroup.TryGetValue(g.GroupId, out var titles);
            buildingsByGroup.TryGetValue(g.GroupId, out var buildings);

            titles ??= [];
            buildings ??= [];
            depreciationByGroup.TryGetValue(g.GroupId, out var deps);
            deps ??= [];

            // Land description (titles only — no building clause).
            var landParts = new List<string>();
            foreach (var title in titles)
            {
                var titleParts = new List<string>();

                if (!string.IsNullOrWhiteSpace(title.TitleNumber))
                    titleParts.Add($"โฉนดที่ดินเลขที่ {title.TitleNumber}");
                if (!string.IsNullOrWhiteSpace(title.BookNumber))
                    titleParts.Add($"เล่ม {title.BookNumber}");
                if (!string.IsNullOrWhiteSpace(title.PageNumber))
                    titleParts.Add($"หน้า {title.PageNumber}");
                if (!string.IsNullOrWhiteSpace(title.LandParcelNumber))
                    titleParts.Add($"เลขที่ดิน {title.LandParcelNumber}");
                if (!string.IsNullOrWhiteSpace(title.SurveyNumber))
                    titleParts.Add($"หน้าสำรวจ {title.SurveyNumber}");
                if (!string.IsNullOrWhiteSpace(title.MapSheetNumber))
                    titleParts.Add($"ระวาง {title.MapSheetNumber}");

                var titleArea = BuildAreaString(title.AreaRai, title.AreaNgan, title.AreaSquareWa);
                if (!string.IsNullOrWhiteSpace(titleArea))
                    titleParts.Add($"เนื้อที่ {titleArea}");

                var addressTail = ThaiAddressFormatter.FormatLandBuilding(
                    houseNumber: null, village: null, moo: null, soi: null, road: null,
                    subDistrict: title.SubDistrict, district: title.District, province: title.Province);
                if (!string.IsNullOrWhiteSpace(addressTail))
                    titleParts.Add(addressTail);

                if (titleParts.Count > 0)
                    landParts.Add(string.Join(" ", titleParts));
            }
            var landDescription = landParts.Count > 0 ? string.Join(" ", landParts) : null;

            // Descriptive building clause (used by the market/combined view).
            var buildingDescParts = new List<string>();
            foreach (var bld in buildings)
            {
                var bldParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(bld.BuildingTypeDisplay))
                    bldParts.Add($"พร้อม{bld.BuildingTypeDisplay}");
                if (bld.NumberOfFloors.HasValue)
                    bldParts.Add($"{bld.NumberOfFloors:0.##} ชั้น");
                if (!string.IsNullOrWhiteSpace(bld.HouseNumber))
                    bldParts.Add($"เลขที่ {bld.HouseNumber}");
                if (bld.TotalBuildingArea.HasValue)
                    bldParts.Add($"พื้นที่ใช้สอย {bld.TotalBuildingArea:0.##} ตารางเมตร");
                if (bld.BuildingAge.HasValue)
                    bldParts.Add($"อายุอาคาร {bld.BuildingAge} ปี");
                if (!string.IsNullOrWhiteSpace(bld.BuildingConditionDisplay))
                    bldParts.Add($"สภาพอาคาร{bld.BuildingConditionDisplay}");
                if (bldParts.Count > 0)
                    buildingDescParts.Add(string.Join(" ", bldParts));
            }

            var collateralDetails = string.Join(" ",
                new[] { landDescription }.Concat(buildingDescParts).Where(s => !string.IsNullOrWhiteSpace(s)));
            collateralDetails = string.IsNullOrWhiteSpace(collateralDetails) ? null : collateralDetails;

            // Building clause shown separately (below the land-title list) in the
            // market/combined row — newline-joined so multiple buildings line-break.
            var buildingDescription = buildingDescParts.Count > 0
                ? string.Join("\n", buildingDescParts)
                : null;

            var totalRai = titles.Sum(t => t.AreaRai ?? 0m);
            var totalNgan = titles.Sum(t => t.AreaNgan ?? 0m);
            var totalSqwa = titles.Sum(t => t.AreaSquareWa ?? 0m);
            var areaOrUnit = titles.Count > 0
                ? BuildAreaString(
                    totalRai == 0m ? null : totalRai,
                    totalNgan == 0m ? null : totalNgan,
                    totalSqwa == 0m ? null : totalSqwa)
                : BuildAreaString(g.AreaRai, g.AreaNgan, g.AreaSquareWa);

            // Total land area in square-wa (rai×400 + ngan×100 + sqwa).
            var totalSquareWa = (totalRai * 400m) + (totalNgan * 100m) + totalSqwa;

            bool isCost = string.Equals(g.ApproachType, "Cost", StringComparison.OrdinalIgnoreCase);

            // Cost-approach breakdown. Buildings: ONE line per building (not per depreciation
            // row) — value = sum of that building's IsBuilding=1 depreciation rows.
            var buildingValueById = deps
                .Where(d => d.IsBuilding)
                .GroupBy(d => d.BuildingAppraisalDetailId)
                .ToDictionary(grp => grp.Key, grp => grp.Sum(d => d.PriceAfterDepreciation ?? 0m));

            var buildingItems = buildings
                .Select(b => new SummaryItemRow
                {
                    Description = BuildBuildingLine(b),
                    Value = buildingValueById.TryGetValue(b.BuildingId, out var v) ? v : (decimal?)null
                })
                .ToList();

            // ส่วนพัฒนา: development items are the non-building (IsBuilding=0) depreciation rows.
            var developmentItems = deps.Where(d => !d.IsBuilding)
                .Select(d => new SummaryItemRow { Description = BuildItemDesc(d, "พื้นที่"), Value = d.PriceAfterDepreciation })
                .ToList();

            var groupTotal = g.GroupAppraisalValue ?? g.AppraisalPrice ?? g.FinalValueRounded;

            // Group composition — also drives the first-column label (see DeriveFamily).
            var hasLand = titles.Count > 0 || g.LandValue.HasValue;
            var hasBuilding = buildingItems.Count > 0 || buildings.Count > 0;

            // Market/combined row only: show พื้นที่ + ราคาต่อหน่วย for a LAND-ONLY group priced at a
            // per-unit rate, where the rate genuinely prices the land. A Land+Building market group
            // has a single blended figure with nothing to divide, so both cells stay blank.
            // Mirrors Appraisal.Domain.Services.PricingUnit.IsPerUnitRate — Reporting cannot
            // reference the Appraisal assembly. KEEP IN SYNC with PricingUnit.cs.
            var isLandRate = g.UnitType is "PerSqWa" or "PerSqm";

            // Land area is title-derived on both sides (the save path resolves it via
            // PricingPropertyDataService.GetTotalLandAreaFromTitlesAsync over the same LandTitles),
            // so totalSquareWa equals PricingFinalValues.LandArea by construction — and it is
            // populated for historical rows that predate that fix.
            var marketLandArea = totalSquareWa == 0m ? null : (decimal?)totalSquareWa;
            var marketLandUnitPrice = g.ValuePerUnit ?? g.LandUnitPrice;   // else FinalValueAdjusted

            var showLandUnitColumns = !isCost
                                      && hasLand
                                      && !hasBuilding
                                      && isLandRate
                                      && g.IncludeLandArea != false
                                      && marketLandArea.HasValue
                                      && marketLandUnitPrice.HasValue;

            return new SummaryGroupRow
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                PropertyType = TranslateCollateralType(DeriveFamily(g.PropertyType, hasLand, hasBuilding)),
                CollateralDetails = collateralDetails,
                AreaOrUnit = areaOrUnit,
                PricePerAreaOrUnit = g.LandUnitPrice,
                AppraisalValue = groupTotal,
                Condition = null,
                Remark = null,
                IsCostApproach = isCost,
                HasLand = hasLand,
                HasBuilding = hasBuilding,
                ShowLandUnitColumns = showLandUnitColumns,
                MarketLandArea = marketLandArea,
                MarketLandUnitPrice = marketLandUnitPrice,
                LandDescription = landDescription,
                LandDescriptions = landParts,
                BuildingDescription = buildingDescription,
                TotalSquareWa = totalSquareWa == 0m ? null : totalSquareWa,
                LandUnitPrice = g.LandUnitPrice,
                LandValue = g.LandValue,
                Buildings = buildingItems,
                DevelopmentItems = developmentItems,
                GroupTotal = groupTotal
            };
        }).ToList();

        // ── Derived fields ───────────────────────────────────────────────────────
        // ราคาประเมินเดิม — prior appraisal's live appraised value (RS23). Shown whenever the
        // appraisal has a PrevAppraisalId, regardless of AppraisalType: any appraisal carrying a
        // prior-appraisal link has a meaningful previous value, and keying off AppraisalType alone
        // hid it for reappraisal chains not typed exactly "ReAppraisal".
        decimal? oldAppraisalValue = prevAppraisal?.PrevAppraisedValue;
        bool hasPrevAppraisal = prevAppraisal?.PrevAppraisalId is not null;

        bool isReAppraisal = string.Equals(
            header.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase);

        // Purpose "02" = increase credit limit → show วงเงินสินเชื่อเดิม (existing) and relabel
        // the loan row to ขอเพิ่มวงเงิน (additional limit). Both amounts from the Request detail.
        bool isIncreaseLimit = string.Equals(header.PurposeCode, "02", StringComparison.OrdinalIgnoreCase);
        decimal? existingLoanValue = requestAddress?.PreviousFacilityLimit;

        // Loan-row amount: ขอเพิ่มวงเงิน (additional) when increasing, else the facility limit.
        // Hidden entirely for reappraisal (handled in the template).
        decimal? loanValue = isIncreaseLimit
            ? requestAddress?.AdditionalFacilityLimit
            : header.FacilityLimit;

        // สภาพที่ดิน — landfill of the first land property per group, as "กลุ่มที่ X {fill}".
        var landFillByGroup = landFillRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(
                gp => gp.Key,
                gp => gp.OrderBy(r => r.SequenceInGroup)
                        .Select(r => r.LandFill)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));

        bool multiGroup = groupRows.Count > 1;
        var landConditions = groupRows
            .Where(g => landFillByGroup.TryGetValue(g.GroupId, out var f) && !string.IsNullOrWhiteSpace(f))
            .Select(g => multiGroup
                ? $"กลุ่มที่ {g.GroupNumber} {landFillByGroup[g.GroupId]}"
                : landFillByGroup[g.GroupId]!)
            .ToList();
        var landCondition = landConditions.Count > 0 ? string.Join(", ", landConditions) : null;

        // การใช้ประโยชน์ — land use of the first land property per group, "กลุ่มที่ X {use}" per line.
        var landUseByGroup = landFillRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(
                gp => gp.Key,
                gp => gp.OrderBy(r => r.SequenceInGroup)
                        .Select(r => ParameterCodeFormatter.DecodeJsonArray(r.LandUseType, r.LandUseTypeOther, landUseMap))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
        var utilizations = groupRows
            .Where(g => landUseByGroup.TryGetValue(g.GroupId, out var u) && !string.IsNullOrWhiteSpace(u))
            .Select(g => multiGroup
                ? $"กลุ่มที่ {g.GroupNumber} {landUseByGroup[g.GroupId]}"
                : landUseByGroup[g.GroupId]!)
            .ToList();

        // ราคาประเมินราชการ — government price per sq.wa, grouped by same price. With >1 distinct
        // price, list the title numbers per price ("โฉนด… และ … ตารางวาละ X บาท") joined by " , ".
        var govPriceGroups = govPriceRows
            .Where(r => r.GovernmentPricePerSqWa.HasValue)
            .GroupBy(r => r.GovernmentPricePerSqWa!.Value)
            .ToList();
        string? governmentPriceText =
            govPriceGroups.Count == 0 ? null
            : govPriceGroups.Count == 1 ? $"ตารางวาละ {govPriceGroups[0].Key:N2} บาท"
            : string.Join(" , ", govPriceGroups.Select(g =>
            {
                var titles = string.Join(" และ ", g
                    .Where(r => !string.IsNullOrWhiteSpace(r.TitleNumber))
                    .Select(r => r.TitleNumber));
                return string.IsNullOrWhiteSpace(titles)
                    ? $"ตารางวาละ {g.Key:N2} บาท"
                    : $"โฉนดที่ดินเลขที่ {titles} ตารางวาละ {g.Key:N2} บาท";
            }));

        // Show the committee block only when this appraisal actually falls into a meeting.
        bool showMeeting = review?.MeetingId is not null;

        // Property type — distinct PropertyType values from the Request's properties,
        // translated to Thai; used for BOTH the header field and the opinion section.
        var propertyTypeLabel = string.Join(", ",
            requestPropertyTypes
                .Select(TranslateCollateralType)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct());
        string? firstPropertyType = string.IsNullOrWhiteSpace(propertyTypeLabel)
            ? summaryGroups.FirstOrDefault()?.PropertyType
            : propertyTypeLabel;

        // Title varies by collateral: land-only (no buildings) → "…ราคาที่ดิน",
        // otherwise the standard "…ราคาทรัพย์สิน". (Reference doc P1 land-only form.)
        bool hasAnyLand = land is not null || groupTitleRows.Count > 0;
        bool hasAnyBuilding = groupBuildingRows.Count > 0;
        string reportTitle = hasAnyLand && !hasAnyBuilding
            ? "สรุปรายงานการประเมินราคาที่ดิน"
            : "สรุปรายงานการประเมินราคาทรัพย์สิน";

        // ที่ดินพร้อมสิ่งปลูกสร้าง — drives the กรรมสิทธิ์สิ่งปลูกสร้าง fallback below.
        bool isLandBuildingAppraisal = hasAnyLand && hasAnyBuilding;

        string? utilization = ParameterCodeFormatter.DecodeJsonArray(
            land?.LandUseType, land?.LandUseTypeOther, landUseMap);

        // วิธีการประเมิน — scoped to the methods of the (filtered) groups actually shown.
        var groupMethodTypes = methodRows
            .GroupBy(r => r.GroupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)g.Select(x => x.MethodType)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        var methodFlags = AppraisalSummaryCommonLoader.FlagsForGroups(
            groupMethodTypes, groupRows.Select(g => g.GroupId));

        // Grand total = sum of the groups actually shown in this report (so the per-group
        // subtotals add up); fall back to the appraisal-level total only when no groups.
        decimal? totalAppraisalValue = summaryGroups.Count > 0
            ? summaryGroups.Sum(g => g.AppraisalValue ?? 0m)
            : valuation?.AppraisedValue;

        // Force-sale rate resolution: appraisal override (valuation.ForceSaleRate) -> system
        // default, passed in by the caller since this method is static and cannot inject
        // ISystemConfigurationReader. Only used as a fallback when ForcedSaleValue itself is
        // not yet persisted (e.g. no PricingAnalysis committed yet).
        var forceSaleRate = valuation?.ForceSaleRate ?? forceSaleRateDefault;
        decimal? forcedSaleValue = valuation?.ForcedSaleValue
            ?? (totalAppraisalValue.HasValue ? totalAppraisalValue.Value * forceSaleRate / 100m : null);

        decimal? buildingCoverage = valuation?.InsuranceValue;

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            ReportTitle = reportTitle,
            AppraisalBookNumber = header.AppraisalNumber,
            AppraisalDate = appraisalDate,
            CustomerName = customerName,
            AoName = aoName,
            AppraisalPurpose = header.AppraisalPurpose,
            PropertyType = firstPropertyType,
            SummaryPropertyType = firstPropertyType,
            CollateralAddress = string.IsNullOrEmpty(collateralAddress) ? null : collateralAddress,
            AdministrativeDistrict = land?.SubDistrict,
            LandOffice = land?.LandOffice,
            OldAppraisalValue = oldAppraisalValue,
            HasPrevAppraisal = hasPrevAppraisal,
            IsReAppraisal = isReAppraisal,
            IsIncreaseLimit = isIncreaseLimit,
            ExistingLoanValue = existingLoanValue,
            Appraiser = appraiser,
            LoanValue = loanValue,
            Groups = summaryGroups,
            TotalAppraisalValue = totalAppraisalValue,
            BuildingCoverageAmount = buildingCoverage,
            ForcedSaleValue = forcedSaleValue,
            Condition = decision?.Condition,
            Remark = decision?.Remark,
            LandOwner = land?.OwnerName,
            EntryExitRights = ParameterCodeFormatter.DecodeJsonArray(
                land?.LandEntranceExitType, land?.LandEntranceExitTypeOther, entranceExitMap),
            // Building ownership is its own column on BuildingAppraisalDetails. It only falls back to
            // the land owner on ที่ดินพร้อมสิ่งปลูกสร้าง (LB), where the two are one collateral unit
            // and a blank building owner means "same as the land" rather than "unknown". Land-only /
            // building-only appraisals keep the two rows independent, so a blank there stays blank
            // (rendered as "-" by .kv .v:empty::before) instead of echoing an unrelated name.
            BuildingOwner = groupBuildingRows
                .Select(b => b.OwnerName)
                .FirstOrDefault(o => !string.IsNullOrWhiteSpace(o))
                ?? (isLandBuildingAppraisal ? land?.OwnerName : null),
            LandCondition = landCondition,
            LandConditions = landConditions,
            Obligation = land?.ObligationDetails,
            CityPlan = land?.UrbanPlanningType,
            Gps = gps,
            GovernmentAssessedValue = land?.GovernmentPrice,
            GovernmentPriceText = governmentPriceText,
            Utilization = utilization,
            Utilizations = utilizations,
            IsWqs = methodFlags.IsWqs,
            IsSaleGrid = methodFlags.IsSaleGrid,
            IsCost = methodFlags.IsCost,
            IsIncome = methodFlags.IsIncome,
            IsHypothesis = methodFlags.IsHypothesis,
            IsLeasehold = methodFlags.IsLeasehold,
            IsProfitRent = methodFlags.IsProfitRent,
            AppraiserComment = AppraisalSummaryCommonLoader.FirstNonBlank(decision?.AppraiserOpinion),
            AppraisalStaffName = staffName,
            AppraisalStaffPosition = staffPosition,
            AppraisalCheckerName = checkerName,
            AppraisalCheckerPosition = checkerPosition,
            AppraisalVerifyName = verifyName,
            AppraisalVerifyPosition = verifyPosition,
            MeetingNumber = review?.MeetingNo,
            MeetingDate = review?.MeetingDate,
            ApprovalDate = approvalDate,
            IsCompleted = isCompleted,
            ShowMeeting = showMeeting,
            ApproverDecisionApproved = approverDecisionApproved,
            Approvers = approvers,
            ApproverSummaryComment = decision?.CommitteeOpinion
        };

        return model;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string BuildAreaString(decimal? rai, decimal? ngan, decimal? sqwa)
        // Thai land-area shorthand rai-ngan-wa; every empty position shows 0 (e.g. "9-2-41 ไร่", "1-0-0 ไร่").
        => $"{rai ?? 0:0.##}-{ngan ?? 0:0.##}-{sqwa ?? 0:0.##} ไร่";

    /// <summary>
    /// Builds a cost-breakdown line, e.g. "อาคารโรงงานชั้นเดียว พื้นที่ใช้สอย 1,800 ตารางเมตร อายุ 9 ปี".
    /// <paramref name="areaLabel"/> is "พื้นที่ใช้สอย" for buildings, "พื้นที่" for development items.
    /// </summary>
    private static string? BuildItemDesc(GroupDepreciationRow d, string areaLabel)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(d.AreaDescription))
            parts.Add(d.AreaDescription!.Trim());
        if (d.Area is { } a && a != 0)
            parts.Add($"{areaLabel} {a:0.##} ตารางเมตร");
        if (d.Year > 0)
            parts.Add($"อายุ {d.Year} ปี");
        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

    /// <summary>
    /// Builds a per-building cost line: property name (type) + floors + usable area + age + condition,
    /// e.g. "อาคารโรงงาน ชั้นเดียว พื้นที่ใช้สอย 1,800 ตารางเมตร อายุอาคาร 9 ปี สภาพอาคารปานกลาง".
    /// </summary>
    private static string? BuildBuildingLine(GroupBuildingRow b)
    {
        var parts = new List<string>();
        // Lead with the building's property name; fall back to the building type.
        var name = !string.IsNullOrWhiteSpace(b.PropertyName) ? b.PropertyName : b.BuildingTypeDisplay;
        if (!string.IsNullOrWhiteSpace(name))
            parts.Add(name!.Trim());
        if (b.NumberOfFloors.HasValue)
            parts.Add($"{b.NumberOfFloors:0.##} ชั้น");
        if (b.TotalBuildingArea.HasValue)
            parts.Add($"พื้นที่ใช้สอย {b.TotalBuildingArea:0.##} ตารางเมตร");
        if (b.BuildingAge.HasValue)
            parts.Add($"อายุอาคาร {b.BuildingAge} ปี");
        if (!string.IsNullOrWhiteSpace(b.BuildingConditionDisplay))
            parts.Add($"สภาพอาคาร{b.BuildingConditionDisplay}");
        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

    private static string? JsonArrayToDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (!s.StartsWith('['))
            return s;

        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(s);
            if (items is null || items.Count == 0)
                return null;
            var joined = string.Join(", ", items.Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
        catch (System.Text.Json.JsonException)
        {
            return s;
        }
    }

    private static Dictionary<string, string?> ToParamMap(IEnumerable<ParamRow> rows) =>
        rows.Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);


    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class HeaderRow
    {
        public string? AppraisalNumber { get; init; }
        public Guid RequestId { get; init; }
        public string? AppraisalType { get; init; }
        public string? AppraisalStatus { get; init; }
        public string? PurposeCode { get; init; }
        public string? AppraisalPurpose { get; init; }
        public decimal? FacilityLimit { get; init; }
    }

    private sealed class LandRow
    {
        public string? OwnerName { get; init; }
        public string? UrbanPlanningType { get; init; }
        public string? ObligationDetails { get; init; }
        public string? Village { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? LandUseType { get; init; }
        public string? LandUseTypeOther { get; init; }
        public string? LandEntranceExitType { get; init; }
        public string? LandEntranceExitTypeOther { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? LandOffice { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public Guid? LandGroupId { get; init; }
        public decimal? GovernmentPrice { get; init; }
    }

    private sealed class RequestAddressRow
    {
        public string? HouseNumber { get; init; }
        public string? ProjectName { get; init; }
        public string? Moo { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public decimal? FacilityLimit { get; init; }
        public decimal? AdditionalFacilityLimit { get; init; }
        public decimal? PreviousFacilityLimit { get; init; }
    }

    private sealed class AssignmentRow
    {
        public string? AssignmentType { get; init; }
        public string? AssigneeCompanyId { get; init; }
        public string? InternalAppraiserId { get; init; }
    }


    private sealed class ValuationRow
    {
        public decimal? AppraisedValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? InsuranceValue { get; init; }
        public decimal? ForceSaleRate { get; init; }
    }

    private sealed class MethodTypeRow
    {
        public Guid GroupId { get; init; }
        public string? MethodType { get; init; }
    }

    private sealed class GroupRow
    {
        public Guid GroupId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public decimal? GroupAppraisalValue { get; init; }   // PricingAnalysis.FinalAppraisedValue
        public string? ApproachType { get; init; }            // "Cost" | "Market" | "Income" | "Residual"
        public decimal? LandValue { get; init; }
        public decimal? BuildingValue { get; init; }
        public decimal? LandUnitPrice { get; init; }          // FinalValueAdjusted
        public decimal? AppraisalPrice { get; init; }
        public decimal? FinalValueRounded { get; init; }
        public decimal? ValuePerUnit { get; init; }           // PricingAnalysisMethods.ValuePerUnit
        public string? UnitType { get; init; }                // "PerSqWa" | "PerSqm" | "PerUnit"
        public bool? IncludeLandArea { get; init; }
        public string? PropertyType { get; init; }
        public string? FirstTitleNumber { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
    }

    private sealed class GroupDepreciationRow
    {
        public Guid PropertyGroupId { get; init; }
        public Guid BuildingAppraisalDetailId { get; init; }
        public bool IsBuilding { get; init; }
        public string? AreaDescription { get; init; }
        public decimal? Area { get; init; }
        public short Year { get; init; }
        public decimal? PriceAfterDepreciation { get; init; }
    }

    private sealed class GroupLandFillRow
    {
        public Guid PropertyGroupId { get; init; }
        public int SequenceInGroup { get; init; }
        public string? LandFill { get; init; }
        public string? LandUseType { get; init; }
        public string? LandUseTypeOther { get; init; }
    }

    private sealed class GovPriceRow
    {
        public string? TitleNumber { get; init; }
        public decimal? GovernmentPricePerSqWa { get; init; }
    }

    private sealed class DecisionRow
    {
        public string? AppraiserOpinion { get; init; }
        public string? CommitteeOpinion { get; init; }
        public string? Condition { get; init; }
        public string? Remark { get; init; }
    }

    private sealed class ReviewRow
    {
        public Guid ReviewId { get; init; }
        public Guid? MeetingId { get; init; }
        public string? MeetingNo { get; init; }
        public DateTime? MeetingDate { get; init; }
    }

    private sealed class VoteRow
    {
        public string? MemberName { get; init; }
        public string? Position { get; init; }
        public string? Vote { get; init; }
        public string? Comment { get; init; }
        public string? Member { get; init; }
        public DateTime VotedAt { get; init; }
    }

    private sealed class RequestorRow
    {
        public string? Requestor { get; init; }
        public string? RequestorName { get; init; }
    }

    private sealed class AoUserRow
    {
        public string? FullName { get; init; }
        public string? Department { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    private sealed class CompletedTaskRow
    {
        public string? ActivityId { get; init; }
        public string? FullName { get; init; }
        public string? Position { get; init; }
        public DateTime CompletedAt { get; init; }
    }

    private sealed class GroupTitleRow
    {
        public Guid PropertyGroupId { get; init; }
        public int SequenceInGroup { get; init; }
        public Guid TitleId { get; init; }
        public string? TitleNumber { get; init; }
        public string? BookNumber { get; init; }
        public string? PageNumber { get; init; }
        public string? LandParcelNumber { get; init; }
        public string? SurveyNumber { get; init; }
        public string? MapSheetNumber { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
    }

    private sealed class GroupBuildingRow
    {
        public Guid PropertyGroupId { get; init; }
        public Guid BuildingId { get; init; }
        public string? PropertyName { get; init; }
        public string? OwnerName { get; init; }
        public int SequenceInGroup { get; init; }
        public string? BuildingTypeDisplay { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? HouseNumber { get; init; }
        public decimal? TotalBuildingArea { get; init; }
        public int? BuildingAge { get; init; }
        public string? BuildingConditionDisplay { get; init; }
    }
}
