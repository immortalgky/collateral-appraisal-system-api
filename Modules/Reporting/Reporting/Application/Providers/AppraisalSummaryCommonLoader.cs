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
    private const decimal Group3FacilityThreshold = 30_000_000m;

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

            -- RS06: Q9 — Per-group skeleton rows
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
                (SELECT COUNT(*)
                 FROM appraisal.PropertyGroupItems gi3
                 WHERE gi3.PropertyGroupId = pg.Id) AS PropertyCount
            FROM appraisal.PropertyGroups pg
            LEFT JOIN appraisal.ValuationAnalyses vab ON vab.AppraisalId = pg.AppraisalId
            LEFT JOIN appraisal.GroupValuations gv
                ON gv.PropertyGroupId = pg.Id
               AND gv.ValuationAnalysisId = vab.Id
            WHERE pg.AppraisalId = @AppraisalId
            ORDER BY pg.GroupNumber;

            -- RS07: Q10 — Pricing method flags
            SELECT DISTINCT pam.MethodType
            FROM appraisal.PricingAnalysisMethods pam
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId
              AND pa.SubjectType = 0;

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
                  'int-appraisal-check',
                  'int-appraisal-verification',
                  'appraisal-book-verification')
            ORDER BY ct.CompletedAt;

            -- RS12: CollateralType code→Thai map
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'CollateralType' AND [Language] = 'TH' AND IsActive = 1;
            """;

        var headerParams = new DynamicParameters();
        headerParams.Add("AppraisalId", appraisalId);

        HeaderRow? header;
        List<string> customerNames;
        DateTime? appraisalDate;
        AssignmentRow? assignment;
        ValuationRow? valuation;
        List<GroupRow> groupRows;
        HashSet<string> methodTypes;
        DecisionRow? decision;
        ReviewRow? review;
        RequestorRow? requestorRow;
        List<CompletedTaskRow> completedTaskRows;
        List<ParamRow> collateralTypeParams;

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
            methodTypes = (await multi.ReadAsync<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        }

        var customerName = customerNames.Count > 0
            ? string.Join(" และ ", customerNames)
            : null;

        // ── Batch 2a: C#-conditional — Q6/Q7 appraiser resolution ───────────────
        // Only one of these two branches fires per call; no way to fold into the
        // batch without scanning both tables unconditionally for every appraisal.
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

                // Q6: Only issued when InternalAppraiserId is set.
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
                // Q7: Only issued when AssigneeCompanyId is a valid Guid.
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

        // ── Batch 2b: C#-conditional — Q12b votes ───────────────────────────────
        // Only issued when a review row exists. Folding into Batch 1 would always
        // scan CommitteeVotes even for appraisals with no review — wasteful and
        // would require joining through the review to get the filter right in one pass.
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

        latestByActivity.TryGetValue("int-appraisal-check", out var checkerTask);
        latestByActivity.TryGetValue("int-appraisal-verification", out var verifyTask);
        if (verifyTask is null)
            latestByActivity.TryGetValue("appraisal-book-verification", out verifyTask);

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
        bool showMeeting = header.FacilityLimit > Group3FacilityThreshold && review is not null;

        decimal? loanValue = !string.Equals(
            header.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase)
            ? header.FacilityLimit
            : null;

        decimal? totalAppraisalValue = valuation?.AppraisedValue
            ?? (groupRows.Count > 0 ? (decimal?)groupRows.Sum(g => g.GroupAppraisalValue ?? 0m) : null);

        decimal? forcedSaleValue = valuation?.ForcedSaleValue
            ?? (totalAppraisalValue.HasValue ? totalAppraisalValue.Value * 0.70m : null);

        return new CommonAppraisalData(
            AppraisalId: appraisalId,
            AppraisalNumber: header.AppraisalNumber,
            RequestId: header.RequestId,
            AppraisalType: header.AppraisalType,
            AppraisalPurpose: header.AppraisalPurpose,
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
            IsWqs: methodTypes.Contains("WQS"),
            IsSaleGrid: methodTypes.Contains("SaleGrid") || methodTypes.Contains("DirectComparison"),
            IsCost: methodTypes.Contains("BuildingCost"),
            IsIncome: methodTypes.Contains("Income"),
            IsHypothesis: methodTypes.Contains("Hypothesis"),
            IsLeasehold: methodTypes.Contains("Leasehold"),
            IsProfitRent: methodTypes.Contains("ProfitRent"),
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
            ShowMeeting: showMeeting,
            GroupRows: groupRows,
            CollateralTypeMap: collateralTypeMap);
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    internal sealed class HeaderRow
    {
        public string? AppraisalNumber { get; init; }
        public Guid RequestId { get; init; }
        public string? AppraisalType { get; init; }
        public string? AppraisalPurpose { get; init; }
        public decimal? FacilityLimit { get; init; }
    }

    internal sealed class AssignmentRow
    {
        public string? AssignmentType { get; init; }
        public string? AssigneeCompanyId { get; init; }
        public string? InternalAppraiserId { get; init; }
    }

    internal sealed class StaffRow
    {
        public string? FullName { get; init; }
        public string? Position { get; init; }
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

/// <summary>
/// Immutable bag of common appraisal-summary data shared across all report variants.
/// </summary>
internal sealed record CommonAppraisalData(
    Guid AppraisalId,
    string? AppraisalNumber,
    Guid RequestId,
    string? AppraisalType,
    string? AppraisalPurpose,
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
    bool IsWqs,
    bool IsSaleGrid,
    bool IsCost,
    bool IsIncome,
    bool IsHypothesis,
    bool IsLeasehold,
    bool IsProfitRent,
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
    bool ShowMeeting,
    List<AppraisalSummaryCommonLoader.GroupRow> GroupRows,
    Dictionary<string, string?> CollateralTypeMap)
{
    /// <summary>
    /// Translate a domain PropertyType family code to its Thai description; fall back to raw code.
    /// (PropertyType stores domain codes, so map family → CollateralType code before lookup.)
    /// </summary>
    public string? TranslateCollateralType(string? code) =>
        CollateralFamilyTranslator.ToThai(code, CollateralTypeMap);
}
