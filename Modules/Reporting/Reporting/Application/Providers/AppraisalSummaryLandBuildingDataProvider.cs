using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;

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
    ILogger<AppraisalSummaryLandBuildingDataProvider> logger)
    : IReportDataProvider
{
    private const decimal Group3FacilityThreshold = 30_000_000m;

    public string ReportTypeKey => "appraisal-summary-land-building";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var model = await BuildAsync(connection, appraisalId, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        // ── Batch 1: 15 result sets, single round-trip ───────────────────────────
        const string batchSql = """
            -- RS01: Q1 — Appraisal header
            SELECT
                a.AppraisalNumber,
                a.RequestId,
                a.AppraisalType,
                a.Purpose          AS AppraisalPurpose,
                a.FacilityLimit
            FROM appraisal.Appraisals a
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

            -- RS04: Q4 — Land detail (first property)
            SELECT TOP 1
                lad.OwnerName,
                lad.UrbanPlanningType,
                lad.ObligationDetails,
                lad.Village,
                lad.Soi,
                lad.Street                AS Road,
                lad.LandUseType,
                lad.LandEntranceExitType,
                lad.SubDistrict,
                lad.District,
                lad.Province,
                lad.LandOffice,
                lad.Latitude,
                lad.Longitude,
                (SELECT TOP 1 lt.GovernmentPrice
                 FROM appraisal.LandTitles lt
                 WHERE lt.LandAppraisalDetailId = lad.Id
                 ORDER BY lt.Id) AS GovernmentPrice
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
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
                va.InsuranceValue
            FROM appraisal.ValuationAnalyses va
            WHERE va.AppraisalId = @AppraisalId;

            -- RS07: Q9 — Per-group valuation rows
            SELECT
                pg.Id                AS GroupId,
                pg.GroupNumber,
                pg.GroupName,
                gv.AppraisedValue    AS GroupAppraisalValue,
                gv.ForcedSaleValue   AS GroupForcedSaleValue,
                gv.ValuePerUnit,
                gv.UnitType,
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
            LEFT JOIN appraisal.ValuationAnalyses vab ON vab.AppraisalId = pg.AppraisalId
            LEFT JOIN appraisal.GroupValuations gv
                ON gv.PropertyGroupId = pg.Id
               AND gv.ValuationAnalysisId = vab.Id
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber;

            -- RS08: Q10 — Pricing method flags
            SELECT DISTINCT pam.MethodType
            FROM appraisal.PricingAnalysisMethods pam
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId
              AND pa.SubjectType = 0;

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
                lad.SubDistrict,
                lad.District,
                lad.Province
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.LandAppraisalDetails lad ON lad.AppraisalPropertyId = ap.Id
            JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup, lt.Id;

            -- RS14: Q16 — Per-group building details (all groups)
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                bad.BuildingType,
                bad.BuildingTypeOther,
                COALESCE(ptBT.Description, bad.BuildingTypeOther, bad.BuildingType)   AS BuildingTypeDisplay,
                bad.NumberOfFloors,
                bad.HouseNumber,
                bad.TotalBuildingArea,
                bad.BuildingAge,
                bad.BuildingConditionType,
                bad.BuildingConditionTypeOther,
                COALESCE(ptBC.Description, bad.BuildingConditionTypeOther, bad.BuildingConditionType) AS BuildingConditionDisplay
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
        HashSet<string> methodTypes;
        DecisionRow? decision;
        ReviewRow? review;
        RequestorRow? requestorRow;
        List<CompletedTaskRow> completedTaskRows;
        List<GroupTitleRow> groupTitleRows;
        List<GroupBuildingRow> groupBuildingRows;
        List<ParamRow> collateralTypeParams;

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

            // RS07
            groupRows = (await multi.ReadAsync<GroupRow>()).ToList();

            // RS08
            methodTypes = (await multi.ReadAsync<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        }

        var customerName = customerNames.Count > 0
            ? string.Join(" และ ", customerNames)
            : null;

        // Build address from land detail
        var collateralAddress = land is not null
            ? ThaiAddressFormatter.FormatLandBuilding(
                houseNumber: null,
                village: land.Village,
                moo: null,
                soi: land.Soi,
                road: land.Road,
                subDistrict: land.SubDistrict,
                district: land.District,
                province: land.Province)
            : null;

        var gps = (land?.Latitude is not null && land.Longitude is not null)
            ? $"{land.Latitude:F6}, {land.Longitude:F6}"
            : null;

        // ── Conditional Q6/Q7: Appraiser resolution ──────────────────────────────
        // Conditional on assignment type — only one branch fires per call.
        string? appraiser = null;
        string? staffName = null;
        string? staffPosition = null;

        if (assignment is not null)
        {
            bool isInternal = string.Equals(
                assignment.AssignmentType, "Internal", StringComparison.OrdinalIgnoreCase);

            if (isInternal)
            {
                appraiser = "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";

                if (!string.IsNullOrWhiteSpace(assignment.InternalAppraiserId))
                {
                    const string staffSql = """
                        SELECT u.FirstName + ' ' + u.LastName AS FullName,
                               u.Position
                        FROM auth.AspNetUsers u
                        WHERE u.UserName = @UserCode
                        """;
                    var staffParams = new DynamicParameters();
                    staffParams.Add("UserCode", assignment.InternalAppraiserId);
                    var staff = await connection.QueryFirstOrDefaultAsync<StaffRow>(staffSql, staffParams);
                    staffName = staff?.FullName;
                    staffPosition = staff?.Position;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(assignment.AssigneeCompanyId)
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
        }

        // ── Conditional Q12b: Committee votes ────────────────────────────────────
        List<ApproverRow> approvers = [];
        bool? approverDecisionApproved = null;

        if (review is not null)
        {
            const string votesSql = """
                SELECT
                    cv.MemberName,
                    cv.MemberRole  AS Position,
                    cv.Vote,
                    cv.Comments    AS Comment
                FROM appraisal.CommitteeVotes cv
                WHERE cv.ReviewId = @ReviewId
                ORDER BY cv.MemberName
                """;

            var voteParams = new DynamicParameters();
            voteParams.Add("ReviewId", review.ReviewId);

            var votes = (await connection.QueryAsync<VoteRow>(votesSql, voteParams)).ToList();

            approvers = votes.Select(v => new ApproverRow
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

            int approveCount = votes.Count(v =>
                string.Equals(v.Vote, "Approve", StringComparison.OrdinalIgnoreCase));
            int rejectCount = votes.Count(v =>
                string.Equals(v.Vote, "Reject", StringComparison.OrdinalIgnoreCase));
            approverDecisionApproved = votes.Count > 0 ? approveCount > rejectCount : null;
        }

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

        latestByActivity.TryGetValue("int-appraisal-check", out var checkerTask);
        latestByActivity.TryGetValue("int-appraisal-verification", out var verifyTask);
        if (verifyTask is null)
            latestByActivity.TryGetValue("appraisal-book-verification", out verifyTask);

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

        var collateralTypeMap = collateralTypeParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(grp => grp.Key, grp => grp.First().Description, StringComparer.OrdinalIgnoreCase);

        // PropertyType stores domain family codes, so map family → CollateralType code before lookup.
        string? TranslateCollateralType(string? code) =>
            CollateralFamilyTranslator.ToThai(code, collateralTypeMap);

        // ── Build per-group summary rows ──────────────────────────────────────────
        var summaryGroups = groupRows.Select(g =>
        {
            titlesByGroup.TryGetValue(g.GroupId, out var titles);
            buildingsByGroup.TryGetValue(g.GroupId, out var buildings);

            titles ??= [];
            buildings ??= [];

            var detailParts = new List<string>();

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
                    houseNumber: null,
                    village: null,
                    moo: null,
                    soi: null,
                    road: null,
                    subDistrict: title.SubDistrict,
                    district: title.District,
                    province: title.Province);
                if (!string.IsNullOrWhiteSpace(addressTail))
                    titleParts.Add(addressTail);

                if (titleParts.Count > 0)
                    detailParts.Add(string.Join(" ", titleParts));
            }

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
                    detailParts.Add(string.Join(" ", bldParts));
            }

            var collateralDetails = detailParts.Count > 0
                ? string.Join(" ", detailParts)
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

            return new SummaryGroupRow
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                PropertyType = TranslateCollateralType(g.PropertyType),
                CollateralDetails = collateralDetails,
                AreaOrUnit = areaOrUnit,
                PricePerAreaOrUnit = g.ValuePerUnit,
                AppraisalValue = g.GroupAppraisalValue,
                Condition = null,
                Remark = null
            };
        }).ToList();

        // ── Derived fields ───────────────────────────────────────────────────────
        decimal? oldAppraisalValue = null;

        decimal? loanValue = !string.Equals(
            header.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase)
            ? header.FacilityLimit
            : null;

        bool showMeeting = header.FacilityLimit > Group3FacilityThreshold && review is not null;

        string? firstPropertyType = summaryGroups.FirstOrDefault()?.PropertyType;

        string? utilization = JsonArrayToDisplay(land?.LandUseType);

        decimal? totalAppraisalValue = valuation?.AppraisedValue
            ?? (summaryGroups.Count > 0 ? summaryGroups.Sum(g => g.AppraisalValue ?? 0m) : null);

        decimal? forcedSaleValue = valuation?.ForcedSaleValue
            ?? (totalAppraisalValue.HasValue ? totalAppraisalValue.Value * 0.70m : null);

        decimal? buildingCoverage = valuation?.InsuranceValue;

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = header.AppraisalNumber,
            AppraisalDate = appraisalDate,
            CustomerName = customerName,
            AoName = aoName,
            AppraisalPurpose = header.AppraisalPurpose,
            PropertyType = firstPropertyType,
            CollateralAddress = string.IsNullOrEmpty(collateralAddress) ? null : collateralAddress,
            AdministrativeDistrict = land?.SubDistrict,
            LandOffice = land?.LandOffice,
            OldAppraisalValue = oldAppraisalValue,
            Appraiser = appraiser,
            LoanValue = loanValue,
            Groups = summaryGroups,
            TotalAppraisalValue = totalAppraisalValue,
            BuildingCoverageAmount = buildingCoverage,
            ForcedSaleValue = forcedSaleValue,
            Condition = decision?.Condition,
            Remark = decision?.Remark,
            LandOwner = land?.OwnerName,
            EntryExitRights = JsonArrayToDisplay(land?.LandEntranceExitType),
            BuildingOwner = land?.OwnerName,
            LandCondition = null,
            Obligation = land?.ObligationDetails,
            CityPlan = land?.UrbanPlanningType,
            Gps = gps,
            GovernmentAssessedValue = land?.GovernmentPrice,
            Utilization = utilization,
            IsWqs = methodTypes.Contains("WQS"),
            IsSaleGrid = methodTypes.Contains("SaleGrid") || methodTypes.Contains("DirectComparison"),
            IsCost = methodTypes.Contains("BuildingCost"),
            IsIncome = methodTypes.Contains("Income"),
            IsHypothesis = methodTypes.Contains("Hypothesis"),
            IsLeasehold = methodTypes.Contains("Leasehold"),
            IsProfitRent = methodTypes.Contains("ProfitRent"),
            AppraiserComment = decision?.AppraiserOpinion ?? decision?.CommitteeOpinion,
            AppraisalStaffName = staffName,
            AppraisalStaffPosition = staffPosition,
            AppraisalCheckerName = checkerName,
            AppraisalCheckerPosition = checkerPosition,
            AppraisalVerifyName = verifyName,
            AppraisalVerifyPosition = verifyPosition,
            MeetingNumber = review?.MeetingNo,
            MeetingDate = review?.MeetingDate,
            ShowMeeting = showMeeting,
            ApproverDecisionApproved = approverDecisionApproved,
            Approvers = approvers,
            ApproverSummaryComment = decision?.CommitteeOpinion
        };

        return model;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string? BuildAreaString(decimal? rai, decimal? ngan, decimal? sqwa)
    {
        if (rai is null && ngan is null && sqwa is null)
            return null;

        var parts = new List<string>();
        if (rai is { } r && r != 0) parts.Add($"{r:0.##} ไร่");
        if (ngan is { } n && n != 0) parts.Add($"{n:0.##} งาน");
        if (sqwa is { } w && w != 0) parts.Add($"{w:0.##} ตร.วา");

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

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class HeaderRow
    {
        public string? AppraisalNumber { get; init; }
        public Guid RequestId { get; init; }
        public string? AppraisalType { get; init; }
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
        public string? LandEntranceExitType { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? LandOffice { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public decimal? GovernmentPrice { get; init; }
    }

    private sealed class AssignmentRow
    {
        public string? AssignmentType { get; init; }
        public string? AssigneeCompanyId { get; init; }
        public string? InternalAppraiserId { get; init; }
    }

    private sealed class StaffRow
    {
        public string? FullName { get; init; }
        public string? Position { get; init; }
    }

    private sealed class ValuationRow
    {
        public decimal? AppraisedValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? InsuranceValue { get; init; }
    }

    private sealed class GroupRow
    {
        public Guid GroupId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public decimal? GroupAppraisalValue { get; init; }
        public decimal? GroupForcedSaleValue { get; init; }
        public decimal? ValuePerUnit { get; init; }
        public string? UnitType { get; init; }
        public string? PropertyType { get; init; }
        public string? FirstTitleNumber { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
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
        public int SequenceInGroup { get; init; }
        public string? BuildingTypeDisplay { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? HouseNumber { get; init; }
        public decimal? TotalBuildingArea { get; init; }
        public int? BuildingAge { get; init; }
        public string? BuildingConditionDisplay { get; init; }
    }
}
