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
///     RS13  prior appraisal link + value
///     RS14  ที่ตั้งทรัพย์สิน anchors (CollateralLocationSql)
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
    /// First non-blank value, or null when every candidate is null/whitespace. Used so a
    /// whitespace-only opinion normalises to null and reaches the template's "-" placeholder
    /// instead of rendering as an empty box (an empty string is truthy in Scriban).
    /// </summary>
    public static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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
        decimal forceSaleRateDefault,
        CancellationToken cancellationToken = default)
    {
        // ── Batch 1: 14 independent result sets (RS01–RS14), single round-trip ───────────────
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
                rd.PreviousFacilityLimit
            FROM appraisal.Appraisals a
            LEFT JOIN parameter.Parameters pPurpose
                ON pPurpose.[group]    = 'AppraisalPurpose'
               AND pPurpose.[language] = 'TH'
               AND pPurpose.[isactive] = 1
               AND pPurpose.[code]     = a.Purpose
            LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
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

            -- RS03: Q3 — Appraisal date. ValuationAnalyses.ValuationDate wins; the latest
            -- non-cancelled appointment across ALL the appraisal's assignments is the fallback
            -- for an appraisal that has no ValuationAnalyses row yet (created on first pricing /
            -- decision-summary save).
            --
            -- ValuationDate must win: an off-system external engagement (company engaged outside
            -- CAS, its book keyed in by an internal appraiser) has NO Appointment row at all, so
            -- appointment-first logic printed a BLANK date on every page of this document despite
            -- the keyer having entered one. In-system cases agree either way — ValuationDate is
            -- re-derived from the latest non-cancelled appointment on every pricing save.
            SELECT COALESCE(
                (SELECT TOP 1 va.ValuationDate
                 FROM appraisal.ValuationAnalyses va
                 WHERE va.AppraisalId = @AppraisalId),
                (SELECT TOP 1 ap.AppointmentDateTime
                 FROM appraisal.Appointments ap
                 JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
                 WHERE aa.AppraisalId = @AppraisalId
                   AND ap.Status <> 'Cancelled'
                 ORDER BY ap.AppointmentDateTime DESC));

            -- RS04: Q5 — Assignment (latest non-rejected/cancelled)
            SELECT TOP 1
                aa.AssignmentType,
                aa.AssigneeCompanyId,
                aa.InternalAppraiserId
            FROM appraisal.AppraisalAssignments aa
            WHERE aa.AppraisalId = @AppraisalId
              AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
            ORDER BY aa.AssignedAt DESC, aa.Id DESC;

            -- RS05: Q8 — Valuation totals + block project force-sale % (for the ForceSaleRate
            -- resolution fallback below). Driven off Appraisals (not ValuationAnalyses) so the
            -- project percentage is still returned even when no ValuationAnalyses row exists yet.
            SELECT
                va.AppraisedValue,
                va.ForcedSaleValue,
                va.InsuranceValue,
                va.ForceSaleRate,
                ppa.ForceSalePercentage AS ProjectForceSalePercentage
            FROM appraisal.Appraisals ap
            LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = ap.Id
            LEFT JOIN appraisal.Projects p ON p.AppraisalId = ap.Id
            LEFT JOIN appraisal.ProjectPricingAssumptions ppa ON ppa.ProjectId = p.Id
            WHERE ap.Id = @AppraisalId AND ap.IsDeleted = 0;

            -- RS06: Q9 — Per-group skeleton rows.
            -- Per-group value comes from the selected PricingAnalysis → Approach → Method →
            -- PricingFinalValue (GroupValuations is not populated by the live flow).
            SELECT
                pg.Id                AS GroupId,
                pg.GroupNumber,
                pg.GroupName,
                COALESCE(pa.FinalAppraisedValue, pfv.EffectiveValue) AS GroupAppraisalValue,
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
            -- Fallback only: pa.FinalAppraisedValue (COALESCE above) is the primary source and is
            -- already correct — it is the domain rollup's sum across a Cost approach's selected
            -- methods (one per role: Land/LandAndBuilding, Building, Machinery). This SUM only fires
            -- when that is NULL. A plain TOP 1 here (as before) is no longer safe: once a Cost
            -- approach can hold 2 selected methods, TOP 1 with no ORDER BY is non-deterministic, so
            -- summing every selected method's own effective value is both correct and stable.
            OUTER APPLY (
                -- MethodValue first (exactly what the rollup sums — e.g. a partial-usage Leasehold's
                -- partial estimate); LEFT JOIN for a method valued on the board with no FinalValue row.
                -- Same read as AppraisalSummaryLandBuildingDataProvider's totalPfv.
                SELECT SUM(COALESCE(pm.MethodValue, fv.IndicatedValue, fv.FinalValue)) AS EffectiveValue
                FROM appraisal.PricingAnalysisApproaches pap
                JOIN appraisal.PricingAnalysisMethods pm
                    ON pm.ApproachId = pap.Id AND pm.IsSelected = 1
                LEFT JOIN appraisal.PricingFinalValues fv
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
                ad.CommitteeOpinion,
                ad.InternalAppraiserOpinion,
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
                  'int-offline-book-keyin',
                  'int-appraisal-check',
                  'int-appraisal-verification',
                  'appraisal-book-verification')
            ORDER BY ct.CompletedAt;

            -- RS12: CollateralType code→Thai map
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'CollateralType' AND [Language] = 'TH' AND IsActive = 1;

            -- RS13: ราคาประเมินเดิม — the prior-appraisal link plus its LIVE appraised value,
            -- resolved through appraisal.Appraisals.PrevAppraisalId (same field as RS05 for the
            -- current appraisal). PrevAppraisalId is returned alongside the value so callers can
            -- distinguish "no prior appraisal" (hide the field) from "prior appraisal exists but
            -- has no valuation yet" (show the field with a dash). LEFT JOIN keeps one row either way.
            SELECT ap.PrevAppraisalId,
                   va.AppraisedValue AS PrevAppraisedValue
            FROM appraisal.Appraisals ap
            LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = ap.PrevAppraisalId
            WHERE ap.Id = @AppraisalId AND ap.IsDeleted = 0;
            """ + CollateralLocationSql;

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
        PrevAppraisalRow? prevAppraisal;
        CollateralLocations locations;

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
            prevAppraisal = await multi.ReadFirstOrDefaultAsync<PrevAppraisalRow>();

            // RS14
            locations = ComposeCollateralLocations(await multi.ReadAsync<CollateralLocationRow>());
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
                // Thai-language document: prefer the Thai company name, fall back to English.
                // Matches the internal branch above, which is already a hardcoded Thai bank name.
                const string companySql = """
                    SELECT COALESCE(NULLIF(c.NameLocal, N''), c.Name) FROM auth.Companies c WHERE c.Id = @CompanyId
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
                av.MemberRole AS MemberRole,
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

        // One row per member (latest vote), in case of re-approval rounds, then ordered by committee
        // rank. The sort has to sit AFTER the GroupBy — GroupBy re-imposes first-appearance order,
        // so an ORDER BY in the SQL would be silently discarded here.
        var votes = (await connection.QueryAsync<VoteRow>(votesSql, voteParams))
            .GroupBy(v => v.Member, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(v => v.VotedAt).First())
            .OrderBy(v => CommitteeRoleRank(v.MemberRole))
            .ThenBy(v => v.VotedAt)
            .ThenBy(v => v.MemberName, StringComparer.Ordinal)
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
        // external = appraisal-book-verification actor — except for an off-system external
        // engagement, which skips book-verification entirely and is keyed at
        // int-offline-book-keyin instead. Fall back to that so the name is not blank.
        CompletedTaskRow? staffTask;
        if (isInternal)
            latestByActivity.TryGetValue("int-appraisal-execution", out staffTask);
        else if (!latestByActivity.TryGetValue("appraisal-book-verification", out staffTask))
            latestByActivity.TryGetValue("int-offline-book-keyin", out staffTask);

        // ผู้ตรวจสอบ / ผู้สอบทาน come only from their own activities; they stay blank
        // until int-appraisal-check / int-appraisal-verification are actually completed.
        latestByActivity.TryGetValue("int-appraisal-check", out var checkerTask);
        latestByActivity.TryGetValue("int-appraisal-verification", out var verifyTask);

        string? staffName = string.IsNullOrWhiteSpace(staffTask?.FullName) ? null : staffTask.FullName!.Trim();
        string? staffPosition = NormalizePosition(staffTask?.Position);
        string? checkerName = string.IsNullOrWhiteSpace(checkerTask?.FullName) ? null : checkerTask.FullName!.Trim();
        string? checkerPosition = NormalizePosition(checkerTask?.Position);
        string? verifyName = string.IsNullOrWhiteSpace(verifyTask?.FullName) ? null : verifyTask.FullName!.Trim();
        string? verifyPosition = NormalizePosition(verifyTask?.Position);

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

        // Force-sale rate resolution: appraisal override -> block project's ForceSalePercentage
        // (ProjectPricingAssumptions, joined in RS05 above) -> system default, passed in by the
        // caller since this loader is static and cannot inject ISystemConfigurationReader. Only
        // used as a fallback when ForcedSaleValue itself is not yet persisted (e.g. no
        // PricingAnalysis committed yet).
        var forceSaleRate = valuation?.ForceSaleRate ?? valuation?.ProjectForceSalePercentage ?? forceSaleRateDefault;
        decimal? forcedSaleValue = valuation?.ForcedSaleValue
            ?? (totalAppraisalValue.HasValue ? totalAppraisalValue.Value * forceSaleRate / 100m : null);

        return new CommonAppraisalData(
            AppraisalId: appraisalId,
            AppraisalNumber: header.AppraisalNumber,
            RequestId: header.RequestId,
            AppraisalType: header.AppraisalType,
            AppraisalPurpose: header.AppraisalPurpose,
            // ที่ตั้งทรัพย์สิน + เขตการปกครอง from the land anchor, else the condo one (RS14); the two
            // share one sub-district so the header lines can never disagree. The condo form reads
            // CondoLocation instead.
            CollateralAddress: locations.LandOrCondo?.Address,
            AdministrativeDistrict: locations.LandOrCondo?.SubDistrict,
            CondoLocation: locations.Condo,
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
            AppraiserComment: FirstNonBlank(decision?.InternalAppraiserOpinion),
            Condition: FirstNonBlank(decision?.Condition),
            Remark: FirstNonBlank(decision?.Remark),
            CommitteeOpinion: FirstNonBlank(decision?.CommitteeOpinion),
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
            PrevAppraisedValue: prevAppraisal?.PrevAppraisedValue,
            HasPrevAppraisal: prevAppraisal?.PrevAppraisalId is not null);
    }

    /// <summary>
    /// Normalizes a sign-off position (auth.AspNetUsers.Position) for display. Blank — or a lone
    /// "-", the placeholder convention used elsewhere in this module — becomes null so the
    /// template drops the line rather than printing a stray dash under someone's name.
    /// </summary>
    internal static string? NormalizePosition(string? position) => ThaiAddressFormatter.Stated(position);

    /// <summary>
    /// Display rank for the committee sign-off block, matching the meeting reports:
    /// Chairman → Director → everyone else → Secretary. Sorts on ApprovalVotes.MemberRole, which is
    /// null on legacy-imported votes, and a Secretary normally cannot vote
    /// (CommitteeMemberPositions.CanVote), so rank 9 is usually an empty slot.
    /// </summary>
    internal static int CommitteeRoleRank(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "chairman" => 1,
        "director" => 2,
        "secretary" => 9,
        _ => 5
    };

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
    }

    /// <summary>
    /// ที่ตั้งทรัพย์สิน source, appended as the LAST result set of every summary batch that needs it
    /// (this loader and AppraisalSummaryLandBuildingDataProvider) so the rule lives in one place.
    /// One row for the first land/building property ('L') and one for the first condo ('U'), by
    /// SequenceNumber. A building-only property (B, LSB) counts as 'L' but is used only when the
    /// appraisal has no land property — it goes to the land-building form and would otherwise
    /// leave the line blank.
    ///   - เลขที่ ← BuildingAppraisalDetails.HouseNumber of the property's own building. Only a
    ///     property with NO building of its own (bare land) borrows from a building-only property
    ///     (B/LSB) in the same group — the house on that plot. Never another LB/LS, another B, or a
    ///     building elsewhere in the appraisal: those are other houses.
    ///   - No ม. segment — no property table has a Moo column.
    ///   - ตำบล/อำเภอ/จังหวัด ← the property's DOPA address, resolved against the DOPA master ONLY.
    ///     A missing or unresolvable code prints blank by decision: never the deed address, never
    ///     the Request.
    /// Block appraisals have no AppraisalProperties, so they get no rows (the block provider
    /// composes its own address from appraisal.Projects).
    /// </summary>
    internal const string CollateralLocationSql = """

            -- RS: ที่ตั้งทรัพย์สิน anchors (see CollateralLocationSql)
            SELECT
                loc.Kind,
                h.HouseNumber,
                loc.Village,
                loc.RoomNumber,
                loc.FloorNumber,
                loc.CondoName,
                loc.Soi,
                loc.Street,
                dsub.NameTh  AS SubDistrict,
                ddist.NameTh AS District,
                dprov.NameTh AS Province
            FROM (
                SELECT ranked.Id, ranked.Kind, ranked.OwnBuildingId, ranked.Village, ranked.RoomNumber,
                       ranked.FloorNumber, ranked.CondoName, ranked.Soi, ranked.Street,
                       ranked.DopaSubDistrict, ranked.DopaDistrict, ranked.DopaProvince
                FROM (
                    SELECT
                        ap.Id,
                        k.Kind,
                        b.Id AS OwnBuildingId,
                        -- A property with land beats a building-only one, whatever the sequence:
                        -- B/LSB is the anchor only when the appraisal has no land at all.
                        ROW_NUMBER() OVER (PARTITION BY k.Kind
                                           ORDER BY CASE WHEN l.Id IS NULL THEN 1 ELSE 0 END,
                                                    ap.SequenceNumber) AS rn,
                        l.Village,
                        c.RoomNumber,
                        c.FloorNumber,
                        c.CondoName,
                        COALESCE(l.Soi, c.Soi)                         AS Soi,
                        COALESCE(l.Street, c.Street)                   AS Street,
                        COALESCE(l.DopaSubDistrict, c.DopaSubDistrict) AS DopaSubDistrict,
                        COALESCE(l.DopaDistrict, c.DopaDistrict)       AS DopaDistrict,
                        COALESCE(l.DopaProvince, c.DopaProvince)       AS DopaProvince
                    FROM appraisal.AppraisalProperties ap
                    LEFT JOIN appraisal.LandAppraisalDetails     l ON l.AppraisalPropertyId = ap.Id
                    LEFT JOIN appraisal.CondoAppraisalDetails    c ON c.AppraisalPropertyId = ap.Id
                    LEFT JOIN appraisal.BuildingAppraisalDetails b ON b.AppraisalPropertyId = ap.Id
                    CROSS APPLY (SELECT CASE WHEN c.Id IS NOT NULL THEN 'U' ELSE 'L' END AS Kind) k
                    WHERE ap.AppraisalId = @AppraisalId
                      AND (l.Id IS NOT NULL OR c.Id IS NOT NULL OR b.Id IS NOT NULL)
                ) ranked
                WHERE ranked.rn = 1  -- anchors only, before the house-number lookup below
            ) loc
            OUTER APPLY (
                -- NoHouseNumber '02' = ยังไม่ขอเลขที่บ้าน (not yet requested); the FE then disables the
                -- house-number field, so the building has no number to print.
                SELECT TOP 1 CASE WHEN b.NoHouseNumber = '02' THEN NULL ELSE b.HouseNumber END AS HouseNumber
                FROM appraisal.BuildingAppraisalDetails b
                JOIN appraisal.AppraisalProperties bp ON bp.Id = b.AppraisalPropertyId
                LEFT JOIN appraisal.PropertyGroupItems bgi ON bgi.AppraisalPropertyId = bp.Id
                LEFT JOIN appraisal.PropertyGroupItems lgi ON lgi.AppraisalPropertyId = loc.Id
                WHERE loc.Kind = 'L'
                  AND bp.AppraisalId = @AppraisalId
                  AND (
                        -- The property's own building is the answer, whatever it says: a blank,
                        -- a dash or NoHouseNumber '02' simply prints no เลขที่.
                        bp.Id = loc.Id
                        -- Bare land only (no building of its own): the first building-only property
                        -- (B/LSB) in the same group — the house standing on that plot — again
                        -- whatever it says. Never another LB/LS: that is a different parcel.
                     OR (loc.OwnBuildingId IS NULL
                         AND bgi.PropertyGroupId = lgi.PropertyGroupId
                         AND NOT EXISTS (SELECT 1 FROM appraisal.LandAppraisalDetails bl
                                         WHERE bl.AppraisalPropertyId = bp.Id)))
                ORDER BY CASE WHEN bp.Id = loc.Id THEN 0 ELSE 1 END,
                         bp.SequenceNumber
            ) h
            LEFT JOIN parameter.DopaSubDistricts dsub  ON dsub.Code  = loc.DopaSubDistrict
            LEFT JOIN parameter.DopaDistricts    ddist ON ddist.Code = loc.DopaDistrict
            LEFT JOIN parameter.DopaProvinces    dprov ON dprov.Code = loc.DopaProvince;
            """;

    internal sealed class CollateralLocationRow
    {
        public string Kind { get; init; } = "";
        public string? HouseNumber { get; init; }
        public string? Village { get; init; }
        public string? RoomNumber { get; init; }
        public string? FloorNumber { get; init; }
        public string? CondoName { get; init; }
        public string? Soi { get; init; }
        public string? Street { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
    }

    /// <summary>
    /// Formats the RS rows of <see cref="CollateralLocationSql"/>: condo → FormatCondo, land → FormatLandBuilding.
    /// Each slot is null when its anchor has nothing to print. Readers use
    /// <see cref="CollateralLocations.LandOrCondo"/> (land-building, book, construction, machine
    /// fallback) or Condo (condo form).
    /// </summary>
    internal static CollateralLocations ComposeCollateralLocations(IEnumerable<CollateralLocationRow> rows)
    {
        CollateralLocation? land = null, condo = null;
        foreach (var r in rows)  // at most one row per Kind
        {
            var address = r.Kind == "U"
                ? ThaiAddressFormatter.FormatCondo(
                    // FloorNumber is free text: 0 means not stated — the condo form body's rule.
                    roomNumber: r.RoomNumber,
                    floorNumber: ThaiAddressFormatter.IsStated(r.FloorNumber) ? r.FloorNumber : null,
                    buildingName: r.CondoName,
                    soi: r.Soi, road: r.Street,
                    subDistrict: r.SubDistrict, district: r.District, province: r.Province)
                : ThaiAddressFormatter.FormatLandBuilding(
                    houseNumber: r.HouseNumber, village: r.Village, moo: null,
                    soi: r.Soi, road: r.Street,
                    subDistrict: r.SubDistrict, district: r.District, province: r.Province);

            // An anchor with nothing to print counts as absent, so a form falls back to the other
            // family's anchor (LandOrCondo). The SQL returns only the first anchor per family, so it
            // never falls back to a later property of the same family. (The address carries the
            // sub-district whenever there is one.)
            if (string.IsNullOrEmpty(address))
                continue;

            // Stated, like the address segments, so เขตการปกครอง and ที่ตั้งทรัพย์สิน agree.
            var location = new CollateralLocation(address, ThaiAddressFormatter.Stated(r.SubDistrict));

            if (r.Kind == "U") condo ??= location;
            else land ??= location;
        }

        return new CollateralLocations(land, condo);
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
        public decimal? ForceSaleRate { get; init; }
        public decimal? ProjectForceSalePercentage { get; init; }
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
        public string? CommitteeOpinion { get; init; }
        public string? InternalAppraiserOpinion { get; init; }
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

        /// <summary>
        /// Committee role (CommitteeMemberPosition name) copied onto the vote from the meeting
        /// roster. Drives display order only — <see cref="Position"/> is what actually prints.
        /// </summary>
        public string? MemberRole { get; init; }
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

    /// <summary>
    /// RS13 / RS23 — the current appraisal's prior-appraisal link and that prior appraisal's live
    /// appraised value. Shared by this loader and AppraisalSummaryLandBuildingDataProvider so the
    /// two queries cannot drift apart.
    /// </summary>
    internal sealed class PrevAppraisalRow
    {
        public Guid? PrevAppraisalId { get; init; }
        public decimal? PrevAppraisedValue { get; init; }
    }
}

/// <summary>วิธีการประเมิน checkbox flags derived from pricing method types.</summary>
internal sealed record MethodFlags(
    bool IsWqs, bool IsSaleGrid, bool IsCost, bool IsIncome,
    bool IsHypothesis, bool IsLeasehold, bool IsProfitRent);

/// <summary>A formatted ที่ตั้งทรัพย์สิน (never empty) and the sub-district (เขตการปกครอง) it was built from.</summary>
internal sealed record CollateralLocation(string Address, string? SubDistrict);

/// <summary>The first land-family anchor and the first condo anchor (each null when there is nothing to print).</summary>
internal sealed record CollateralLocations(CollateralLocation? Land, CollateralLocation? Condo)
{
    /// <summary>
    /// Land first, else the condo: the rule for the land-building form, internal book, construction
    /// summary and the machine form's fallback.
    /// </summary>
    public CollateralLocation? LandOrCondo => Land ?? Condo;
}

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
    string? AdministrativeDistrict,
    CollateralLocation? CondoLocation,
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
    decimal? PrevAppraisedValue,
    /// <summary>True when the appraisal has a PrevAppraisalId — drives whether ราคาประเมินเดิม
    /// is rendered at all, independently of AppraisalType.</summary>
    bool HasPrevAppraisal)
{
    /// <summary>
    /// Translate a domain PropertyType family code to its Thai description; fall back to raw code.
    /// (PropertyType stores domain codes, so map family → CollateralType code before lookup.)
    /// </summary>
    public string? TranslateCollateralType(string? code) =>
        CollateralFamilyTranslator.ToThai(code, CollateralTypeMap);
}
