using Reporting.Application.Formatting;
using Reporting.Application.Models;
using System.Data;

namespace Reporting.Application.Providers;

/// <summary>
/// Loads the ~10 data fragments that are IDENTICAL across all three appraisal-summary
/// report variants (Land&Building, Condo, Machine).
///
/// The caller opens the connection, resolves the appraisalId, and then calls
/// <see cref="LoadAsync"/> which fills a <see cref="CommonAppraisalData"/> record.
/// Each variant provider adds only type-specific queries on top.
///
/// Phase C — QueryMultiple batch:
///   Batch 1 (single round-trip, all off @AppraisalId / @RequestId via subquery):
///     RS01  Q1  appraisal.Appraisals — header
///     RS02  Q2  request.RequestCustomers — customer names (RequestId via subquery)
///     RS03  Q3  appraisal.Appointments — inspection/appraisal date
///     RS04  Q5  appraisal.AppraisalAssignments — assignment
///     RS05  Q8  appraisal.ValuationAnalyses — totals
///     RS06  Q9  appraisal.PropertyGroups + GroupValuations — per-group skeleton rows
///     RS07  Q10 appraisal.PricingAnalysisMethods — method type flags
///     RS08  Q11 appraisal.AppraisalDecisions — decision
///     RS09  Q12 appraisal.AppraisalReviews + Meetings — review row
///     RS10  Q13 request.Requests — requestor (RequestId via subquery)
///     RS11  Q14 workflow.CompletedTasks — checker/verifier (RequestId via subquery)
///     RS12  ColTypeMap parameter.Parameters CollateralType group
///
///   Batch 2 (C#-conditional; only issued when the assignment/review data warrants it):
///     Q6  auth.AspNetUsers — internal staff (only for Internal assignment)
///     Q7  auth.Companies — company name (only for External assignment with Guid CompanyId)
///     Q12b appraisal.CommitteeVotes — votes (only when review row exists)
///     AO user/dept — auth.AspNetUsers (only when RequestorName is blank or dept needed)
/// </summary>
internal static class AppraisalSummaryCommonLoader
{
    /// <summary>
    /// Maps a set of pricing APPROACH types (Market / Cost / Income / Residual) to the four
    /// วิธีการประเมิน checkboxes. Callers pass the approaches of the groups a report actually shows.
    /// Flag names are kept for template compatibility: IsWqs→Market, IsHypothesis→Residual.
    /// </summary>
    public static MethodFlags BuildMethodFlags(IEnumerable<string> approachTypes)
    {
        var s = approachTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new MethodFlags(
            IsWqs: s.Contains("Market"),       // Market Comparison Approach
            IsSaleGrid: false,
            IsCost: s.Contains("Cost"),        // Cost Approach
            IsIncome: s.Contains("Income"),    // Income Approach
            IsHypothesis: s.Contains("Residual"), // Residual Approach
            IsLeasehold: false,
            IsProfitRent: false);
    }

    /// <summary>Union of pricing method types across the given group ids.</summary>
    public static MethodFlags FlagsForGroups(
        IReadOnlyDictionary<Guid, IReadOnlySet<string>> groupMethodTypes,
        IEnumerable<Guid> groupIds)
    {
        var types = groupIds
            .SelectMany(id => groupMethodTypes.TryGetValue(id, out var t) ? t : Enumerable.Empty<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return BuildMethodFlags(types);
    }

    /// <summary>
    /// Loads all common appraisal-summary data for the given <paramref name="appraisalId"/>.
    /// Returns <see langword="null"/> if the appraisal does not exist.
    /// </summary>
    public static async Task<CommonAppraisalData?> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken cancellationToken = default)
    {
        // ── Batch 1: 12 independent result sets, single round-trip ───────────────
        // Q2 / Q13 / Q14 reference RequestId — resolved via scalar subquery
        // so the entire batch uses only @AppraisalId.
        const string batchSql = """
            -- RS01: Q1 — Appraisal header
            SELECT
                a.AppraisalNumber,
                a.RequestId,
                a.AppraisalType,
                a.Status           AS AppraisalStatus,
                a.Purpose          AS PurposeCode,
                COALESCE(pPurpose.[description], a.Purpose) AS AppraisalPurpose,
                a.FacilityLimit,
                rd.AdditionalFacilityLimit,
                rd.PreviousFacilityLimit,
                -- Collateral address (ที่ตั้งทรัพย์สิน) from the Request detail, same as land-building
                rd.HouseNumber,
                rd.ProjectName,
                rd.Moo,
                rd.Soi,
                rd.Road,
                COALESCE(tsub.NameTh,  rd.SubDistrict) AS ReqSubDistrict,
                COALESCE(tdist.NameTh, rd.District)    AS ReqDistrict,
                COALESCE(tprov.NameTh, rd.Province)    AS ReqProvince
            FROM appraisal.Appraisals a
            LEFT JOIN parameter.Parameters pPurpose
                ON pPurpose.[group]    = 'AppraisalPurpose'
               AND pPurpose.[language] = 'TH'
               AND pPurpose.[isactive] = 1
               AND pPurpose.[code]     = a.Purpose
            LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = rd.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = rd.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = rd.SubDistrict
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

            -- RS04: Q5 — Assignment (latest non-rejected/cancelled)
            SELECT TOP 1
                aa.AssignmentType,
                aa.AssigneeCompanyId,
                aa.InternalAppraiserId
            FROM appraisal.AppraisalAssignments aa
            WHERE aa.AppraisalId = @AppraisalId
              AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
            ORDER BY aa.AssignedAt DESC, aa.Id DESC;

            -- RS05: Q8 — Valuation totals
            SELECT
                va.AppraisedValue,
                va.ForcedSaleValue,
                va.InsuranceValue
            FROM appraisal.ValuationAnalyses va
            WHERE va.AppraisalId = @AppraisalId;

            -- RS06: Q9 — Per-group skeleton rows.
            -- Per-group value comes from the selected PricingAnalysis → Approach → Method →
            -- PricingFinalValue (GroupValuations is not populated by the live flow).
            SELECT
                pg.Id                AS GroupId,
                pg.GroupNumber,
                pg.GroupName,
                COALESCE(pa.FinalAppraisedValue, pfv.FinalValueRounded, pfv.AppraisalPrice) AS GroupAppraisalValue,
                (SELECT TOP 1 ap.PropertyType
                 FROM appraisal.PropertyGroupItems gi2
                 JOIN appraisal.AppraisalProperties ap ON ap.Id = gi2.AppraisalPropertyId
                 WHERE gi2.PropertyGroupId = pg.Id
                 ORDER BY gi2.SequenceInGroup) AS PropertyType,
                (SELECT COUNT(*)
                 FROM appraisal.PropertyGroupItems gi3
                 WHERE gi3.PropertyGroupId = pg.Id) AS PropertyCount
            FROM appraisal.PropertyGroups pg
            LEFT JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId = pg.Id AND pa.SubjectType = 0
            OUTER APPLY (
                SELECT TOP 1 fv.FinalValueRounded, fv.AppraisalPrice
                FROM appraisal.PricingAnalysisApproaches pap
                JOIN appraisal.PricingAnalysisMethods pm
                    ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                JOIN appraisal.PricingFinalValues fv
                    ON fv.PricingMethodId = pm.Id
                WHERE pap.PricingAnalysisId = pa.Id AND pap.IsSelected = 1
            ) pfv
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber;

            -- RS07: Q10 — Selected pricing APPROACH per group (drives the วิธีการประเมิน checkboxes)
            SELECT DISTINCT pg.Id AS GroupId, paa.ApproachType AS MethodType
            FROM appraisal.PricingAnalysisApproaches paa
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId
              AND pa.SubjectType = 0
              AND paa.IsSelected = 1;

            -- RS08: Q11 — Appraisal decision
            SELECT
                ad.AppraiserOpinion,
                ad.CommitteeOpinion,
                ad.Condition,
                ad.Remark
            FROM appraisal.AppraisalDecisions ad
            WHERE ad.AppraisalId = @AppraisalId;

            -- RS09: Q12 — Review + meeting row
            SELECT
                ar.Id             AS ReviewId,
                ar.MeetingId,
                m.MeetingNo,
                m.StartAt         AS MeetingDate
            FROM appraisal.AppraisalReviews ar
            LEFT JOIN workflow.Meetings m ON m.Id = ar.MeetingId
            WHERE ar.AppraisalId = @AppraisalId;

            -- RS10: Q13 — Requestor (RequestId via subquery)
            SELECT r.Requestor, r.RequestorName
            FROM request.Requests r
            WHERE r.Id = (
                SELECT a3.RequestId
                FROM appraisal.Appraisals a3
                WHERE a3.Id = @AppraisalId AND a3.IsDeleted = 0);

            -- RS11: Q14 — Checker/verifier completed tasks (RequestId via subquery)
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

            -- RS12: CollateralType code→Thai map
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'CollateralType' AND [Language] = 'TH' AND IsActive = 1;

            -- RS13: ราคาประเมินเดิม — live appraised value of the prior appraisal (reappraisal chain),
            -- resolved through appraisal.Appraisals.PrevAppraisalId (same field as RS05 for the current
            -- appraisal); null when there is no prior appraisal or it has no valuation.
            SELECT va.AppraisedValue
            FROM appraisal.ValuationAnalyses va
            WHERE va.AppraisalId = (
                SELECT ap.PrevAppraisalId FROM appraisal.Appraisals ap
                WHERE ap.Id = @AppraisalId AND ap.IsDeleted = 0);
            """;

        var headerParams = new DynamicParameters();
        headerParams.Add("AppraisalId", appraisalId);

        HeaderRow? header;
        List<string> customerNames;
        DateTime? appraisalDate;
        AssignmentRow? assignment;
        ValuationRow? valuation;
        List<GroupRow> groupRows;
        List<GroupMethodRow> groupMethodRows;
        DecisionRow? decision;
        ReviewRow? review;
        RequestorRow? requestorRow;
        List<CompletedTaskRow> completedTaskRows;
        List<ParamRow> collateralTypeParams;
        decimal? prevAppraisedValue;

        using (var multi = await connection.QueryMultipleAsync(batchSql, headerParams))
        {
            // RS01
            header = await multi.ReadFirstOrDefaultAsync<HeaderRow>();
            if (header is null)
                return null;

            // RS02
            customerNames = (await multi.ReadAsync<string>()).ToList();

            // RS03
            appraisalDate = await multi.ReadFirstOrDefaultAsync<DateTime?>();

            // RS04
            assignment = await multi.ReadFirstOrDefaultAsync<AssignmentRow>();

            // RS05
            valuation = await multi.ReadFirstOrDefaultAsync<ValuationRow>();

            // RS06
            groupRows = (await multi.ReadAsync<GroupRow>()).ToList();

            // RS07
            groupMethodRows = (await multi.ReadAsync<GroupMethodRow>()).ToList();

            // RS08
            decision = await multi.ReadFirstOrDefaultAsync<DecisionRow>();

            // RS09
            review = await multi.ReadFirstOrDefaultAsync<ReviewRow>();

            // RS10
            requestorRow = await multi.ReadFirstOrDefaultAsync<RequestorRow>();

            // RS11
            completedTaskRows = (await multi.ReadAsync<CompletedTaskRow>()).ToList();

            // RS12
            collateralTypeParams = (await multi.ReadAsync<ParamRow>()).ToList();

            // RS13
            prevAppraisedValue = await multi.ReadFirstOrDefaultAsync<decimal?>();
        }

        var customerName = customerNames.Count > 0
            ? string.Join(" และ ", customerNames)
            : null;

        // Per-group pricing method types (for the วิธีการประเมิน checkboxes, scoped to the
        // groups a given report actually shows); plus a global union for the default flags.
        var groupMethodTypes = groupMethodRows
            .GroupBy(r => r.GroupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)g.Select(x => x.MethodType)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        var methodTypes = groupMethodRows
            .Select(r => r.MethodType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Default (appraisal-wide) flags — used by reports that show all groups (construction/block).
        var globalFlags = BuildMethodFlags(methodTypes);

        // ── Batch 2a: C#-conditional — appraiser (header) resolution ────────────
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
            // Q7: Only issued when AssigneeCompanyId is a valid Guid.
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

        // Approval date (latest vote) for the sub-committee header; completed gates the block.
        DateTime? approvalDate = votes.Count > 0 ? votes.Max(v => v.VotedAt) : (DateTime?)null;
        bool isCompleted = string.Equals(header.AppraisalStatus, "Completed", StringComparison.OrdinalIgnoreCase);

        // ── Q13 / AO name resolution ─────────────────────────────────────────────
        // C#-conditional: issues 0–1 extra query depending on RequestorName presence.
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

        // ── CollateralType map ───────────────────────────────────────────────────
        var collateralTypeMap = collateralTypeParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grp => grp.Key,
                grp => grp.First().Description,
                StringComparer.OrdinalIgnoreCase);

        // ── Derived scalars ──────────────────────────────────────────────────────
        // Show the committee block only when this appraisal actually falls into a meeting.
        bool showMeeting = review?.MeetingId is not null;

        // Purpose "02" = increase credit limit → existing limit (วงเงินสินเชื่อเดิม) + the
        // loan row relabels to ขอเพิ่มวงเงิน (additional limit). Amounts from the Request detail.
        bool isIncreaseLimit = string.Equals(header.PurposeCode, "02", StringComparison.OrdinalIgnoreCase);
        decimal? existingLoanValue = header.PreviousFacilityLimit;

        decimal? loanValue = isIncreaseLimit
            ? header.AdditionalFacilityLimit
            : header.FacilityLimit;

        decimal? totalAppraisalValue = valuation?.AppraisedValue
            ?? (groupRows.Count > 0 ? (decimal?)groupRows.Sum(g => g.GroupAppraisalValue ?? 0m) : null);

        decimal? forcedSaleValue = valuation?.ForcedSaleValue
            ?? (totalAppraisalValue.HasValue ? totalAppraisalValue.Value * 0.70m : null);

        // ที่ตั้งทรัพย์สิน — built from the Request detail, identical to the land-building form.
        var reqCollateralAddress = ThaiAddressFormatter.FormatLandBuilding(
            houseNumber: header.HouseNumber, village: header.ProjectName, moo: header.Moo,
            soi: header.Soi, road: header.Road,
            subDistrict: header.ReqSubDistrict, district: header.ReqDistrict, province: header.ReqProvince);

        return new CommonAppraisalData(
            AppraisalId: appraisalId,
            AppraisalNumber: header.AppraisalNumber,
            RequestId: header.RequestId,
            AppraisalType: header.AppraisalType,
            AppraisalPurpose: header.AppraisalPurpose,
            CollateralAddress: string.IsNullOrEmpty(reqCollateralAddress) ? null : reqCollateralAddress,
            FacilityLimit: header.FacilityLimit,
            CustomerName: customerName,
            AppraisalDate: appraisalDate,
            Appraiser: appraiser,
            StaffName: staffName,
            StaffPosition: staffPosition,
            TotalAppraisalValue: totalAppraisalValue,
            ForcedSaleValue: forcedSaleValue,
            BuildingCoverageAmount: valuation?.InsuranceValue,
            LoanValue: loanValue,
            IsIncreaseLimit: isIncreaseLimit,
            ExistingLoanValue: existingLoanValue,
            IsWqs: globalFlags.IsWqs,
            IsSaleGrid: globalFlags.IsSaleGrid,
            IsCost: globalFlags.IsCost,
            IsIncome: globalFlags.IsIncome,
            IsHypothesis: globalFlags.IsHypothesis,
            IsLeasehold: globalFlags.IsLeasehold,
            IsProfitRent: globalFlags.IsProfitRent,
            GroupMethodTypes: groupMethodTypes,
            AppraiserComment: decision?.AppraiserOpinion ?? decision?.CommitteeOpinion,
            Condition: decision?.Condition,
            Remark: decision?.Remark,
            CommitteeOpinion: decision?.CommitteeOpinion,
            AoName: aoName,
            CheckerName: checkerName,
            CheckerPosition: checkerPosition,
            VerifyName: verifyName,
            VerifyPosition: verifyPosition,
            Review: review,
            Approvers: approvers,
            ApproverDecisionApproved: approverDecisionApproved,
            ApprovalDate: approvalDate,
            IsCompleted: isCompleted,
            ShowMeeting: showMeeting,
            GroupRows: groupRows,
            CollateralTypeMap: collateralTypeMap,
            PrevAppraisedValue: prevAppraisedValue);
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    internal sealed class HeaderRow
    {
        public string? AppraisalNumber { get; init; }
        public Guid RequestId { get; init; }
        public string? AppraisalType { get; init; }
        public string? AppraisalStatus { get; init; }
        public string? PurposeCode { get; init; }
        public string? AppraisalPurpose { get; init; }
        public decimal? FacilityLimit { get; init; }
        public decimal? AdditionalFacilityLimit { get; init; }
        public decimal? PreviousFacilityLimit { get; init; }
        public string? HouseNumber { get; init; }
        public string? ProjectName { get; init; }
        public string? Moo { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? ReqSubDistrict { get; init; }
        public string? ReqDistrict { get; init; }
        public string? ReqProvince { get; init; }
    }

    internal sealed class AssignmentRow
    {
        public string? AssignmentType { get; init; }
        public string? AssigneeCompanyId { get; init; }
        public string? InternalAppraiserId { get; init; }
    }

    internal sealed class ValuationRow
    {
        public decimal? AppraisedValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? InsuranceValue { get; init; }
    }

    internal sealed class GroupRow
    {
        public Guid GroupId { get; init; }
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public decimal? GroupAppraisalValue { get; init; }
        public decimal? GroupForcedSaleValue { get; init; }
        public decimal? ValuePerUnit { get; init; }
        public string? UnitType { get; init; }
        public string? PropertyType { get; init; }
        public int PropertyCount { get; init; }
    }

    internal sealed class GroupMethodRow
    {
        public Guid GroupId { get; init; }
        public string? MethodType { get; init; }
    }

    internal sealed class DecisionRow
    {
        public string? AppraiserOpinion { get; init; }
        public string? CommitteeOpinion { get; init; }
        public string? Condition { get; init; }
        public string? Remark { get; init; }
    }

    internal sealed class ReviewRow
    {
        public Guid ReviewId { get; init; }
        public Guid? MeetingId { get; init; }
        public string? MeetingNo { get; init; }
        public DateTime? MeetingDate { get; init; }
    }

    internal sealed class VoteRow
    {
        public string? MemberName { get; init; }
        public string? Position { get; init; }
        public string? Vote { get; init; }
        public string? Comment { get; init; }
        public string? Member { get; init; }
        public DateTime VotedAt { get; init; }
    }

    internal sealed class RequestorRow
    {
        public string? Requestor { get; init; }
        public string? RequestorName { get; init; }
    }

    internal sealed class AoUserRow
    {
        public string? FullName { get; init; }
        public string? Department { get; init; }
    }

    internal sealed class ParamRow
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    internal sealed class CompletedTaskRow
    {
        public string? ActivityId { get; init; }
        public string? FullName { get; init; }
        public string? Position { get; init; }
        public DateTime CompletedAt { get; init; }
    }
}

/// <summary>วิธีการประเมิน checkbox flags derived from pricing method types.</summary>
internal sealed record MethodFlags(
    bool IsWqs, bool IsSaleGrid, bool IsCost, bool IsIncome,
    bool IsHypothesis, bool IsLeasehold, bool IsProfitRent);

/// <summary>
/// Immutable bag of common appraisal-summary data shared across all report variants.
/// </summary>
internal sealed record CommonAppraisalData(
    Guid AppraisalId,
    string? AppraisalNumber,
    Guid RequestId,
    string? AppraisalType,
    string? AppraisalPurpose,
    string? CollateralAddress,
    decimal? FacilityLimit,
    string? CustomerName,
    DateTime? AppraisalDate,
    string? Appraiser,
    string? StaffName,
    string? StaffPosition,
    decimal? TotalAppraisalValue,
    decimal? ForcedSaleValue,
    decimal? BuildingCoverageAmount,
    decimal? LoanValue,
    bool IsIncreaseLimit,
    decimal? ExistingLoanValue,
    bool IsWqs,
    bool IsSaleGrid,
    bool IsCost,
    bool IsIncome,
    bool IsHypothesis,
    bool IsLeasehold,
    bool IsProfitRent,
    IReadOnlyDictionary<Guid, IReadOnlySet<string>> GroupMethodTypes,
    string? AppraiserComment,
    string? Condition,
    string? Remark,
    string? CommitteeOpinion,
    string? AoName,
    string? CheckerName,
    string? CheckerPosition,
    string? VerifyName,
    string? VerifyPosition,
    AppraisalSummaryCommonLoader.ReviewRow? Review,
    List<ApproverRow> Approvers,
    bool? ApproverDecisionApproved,
    DateTime? ApprovalDate,
    bool IsCompleted,
    bool ShowMeeting,
    List<AppraisalSummaryCommonLoader.GroupRow> GroupRows,
    Dictionary<string, string?> CollateralTypeMap,
    decimal? PrevAppraisedValue)
{
    /// <summary>
    /// Translate a domain PropertyType family code to its Thai description; fall back to raw code.
    /// (PropertyType stores domain codes, so map family → CollateralType code before lookup.)
    /// </summary>
    public string? TranslateCollateralType(string? code) =>
        CollateralFamilyTranslator.ToThai(code, CollateralTypeMap);
}
