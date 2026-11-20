using System.Data;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Providers.Sections;

namespace Reporting.Application.Providers;

/// <summary>
/// Builds the EXTERNAL-company cover-page + company-letter fields of the unified appraisal book
/// (FSD §2.1.2 — เล่มรายงานประเมินบริษัท) onto an <see cref="AppraisalSummaryModel"/>.
///
/// Only the cover/letter fields are set here; the shared detail sections
/// (Land/Building/Condo/Construction/Machine/Comparison/WQS/SaleGrid/CostMachine/Appendix) are
/// loaded ONCE by <see cref="AppraisalBookDataProvider"/> regardless of internal/external, so this
/// builder deliberately does NOT call the section loaders.
///
/// Data is fetched in a single QueryMultiple batch (13 result sets, all off @AppraisalId) plus a
/// conditional company lookup — identical to the former ExternalReportDataProvider.
/// </summary>
internal static class ExternalBookBuilder
{
    private const string BankName = "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";
    private const string LetterSubject = "แจ้งผลการประเมินมูลค่าทรัพย์สิน";
    private const string ExternalAppraisalPurpose =
        "เพื่อใช้ในการพิจารณาขอสินเชื่อของ ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";
    private const string ExternalAppraisalCriteria = "เพื่อทราบมูลค่าตลาด";

    // ── Method type → printed name ───────────────────────────────────────────────
    // Market-comparison methods keep their long Thai name on the "วิธีการประเมินมูลค่า" row; the value table
    // uses the short name next to the approach it belongs to.
    private static readonly IReadOnlyDictionary<string, string> MethodTypeLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WQS"]               = "วิธีเปรียบเทียบราคาตลาด (WQS)",
            ["SaleGrid"]          = "วิธีเปรียบเทียบตลาด (Sale Grid)",
            ["DirectComparison"]  = "วิธีเปรียบเทียบตลาด (Direct Comparison)",
        };

    private static readonly IReadOnlyDictionary<string, string> MethodShortNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WQS"]               = "WQS",
            ["SaleGrid"]          = "Sale Grid",
            ["DirectComparison"]  = "Direct Comparison",
            ["BuildingCost"]      = "ต้นทุนสิ่งปลูกสร้าง",
            ["MachineryCost"]     = "ต้นทุนเครื่องจักร",
            ["Income"]            = "Income",
            ["Leasehold"]         = "Leasehold",
            ["ProfitRent"]        = "Profit Rent",
            ["Hypothesis"]        = "Hypothesis",
        };

    internal sealed record PricingSummary(
        IReadOnlyList<ApproachValueRow> Rows, bool MultiGroup, string? PriceMethod, string? Reason);

    /// <summary>
    /// The approach value when none is stored. A stored non-zero ApproachValue always wins (the caller checks it first). Without one:
    /// Cost = null, exactly like the domain (ComputeSelectedValue returns null when nothing is selected); guessing a combination of the
    /// valued methods would add up alternatives (a LandAndBuilding next to a Building) or mix roles. Any other approach = its first
    /// method that has a value, in the picker's deterministic (creation) order. Null when no method carries a value.
    /// </summary>
    internal static decimal? DeriveApproachValue(string? approachType, IReadOnlyList<PickedMethod> picked) =>
        DerivingMethod(approachType, picked)?.MethodValue;

    /// <summary>The one method a derived (not stored) value comes from: the first valued method of a non-Cost approach; null for Cost or when none has a value.</summary>
    internal static PickedMethod? DerivingMethod(string? approachType, IReadOnlyList<PickedMethod> picked) =>
        string.Equals(approachType, "Cost", StringComparison.OrdinalIgnoreCase)
            ? null
            : picked.FirstOrDefault(m => PricingMethodPicker.HasValue(m.MethodValue));

    /// <summary>A stored non-zero value wins; a stored 0 means none (never printed back); otherwise <see cref="DeriveApproachValue"/>.</summary>
    internal static decimal? ApproachValueOf(decimal? stored, string? approachType, IReadOnlyList<PickedMethod> picked) =>
        PricingMethodPicker.HasValue(stored) ? stored : DeriveApproachValue(approachType, picked);

    /// <summary>
    /// One row per analysed approach of every property group — the approach the valuation used first, then
    /// the reference ones — plus the "วิธีการประเมินมูลค่า" line (selected approaches only) and the appraiser's
    /// reason for choosing (PricingAnalysis.Remark). Which methods stand behind an approach comes from
    /// <see cref="PricingMethodPicker"/>, the same rule the pricing pages use.
    /// </summary>
    internal static async Task<PricingSummary> LoadPricingAsync(
        IDbConnection connection, Guid appraisalId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                pg.GroupNumber,
                pg.GroupName,
                paa.Id            AS ApproachId,
                paa.ApproachType,
                paa.IsSelected,
                paa.ApproachValue,
                pa.Remark
            FROM appraisal.PropertyGroups pg
            JOIN appraisal.PricingAnalysis pa
                ON pa.AnchorId = pg.Id
               AND pa.SubjectType = 0
            JOIN appraisal.PricingAnalysisApproaches paa
                ON paa.PricingAnalysisId = pa.Id
            WHERE pg.AppraisalId = @AppraisalId
            -- Within a group the used approach first, then the order the approaches were created (a v7 Guid's SQL Server sort order is not creation order, so Id is only the last tie-break).
            -- The same order is used by every loader that walks the approaches (PricingMethodPicker, PricingSubAnalysisLoader, ComparisonSectionLoader).
            ORDER BY pg.GroupNumber, paa.IsSelected DESC, CASE WHEN paa.CreatedAt IS NULL THEN 1 ELSE 0 END, paa.CreatedAt, paa.ApproachType, paa.Id
            """;

        var approaches = (await connection.QueryAsync<ApproachRow>(
            new CommandDefinition(sql, new { AppraisalId = appraisalId }, cancellationToken: ct))).ToList();
        if (approaches.Count == 0)
            return new PricingSummary([], false, null, null);

        var methods = await PricingMethodPicker.LoadAsync(connection, appraisalId, MethodShortNames.Keys.ToArray(), ct);
        var multiGroup = approaches.Select(a => a.GroupNumber).Distinct().Count() > 1;

        var rows = new List<ApproachValueRow>();
        var selectedLabels = new List<string>();
        foreach (var a in approaches)
        {
            var mine = methods.Where(m => m.ApproachId == a.ApproachId).ToList();
            // A stored non-zero approach value wins; a stored 0 means none, like the picker's rule (null and 0 both mean no value), and prints
            // as nothing. Without one, a Cost approach has no value (the domain gives null with nothing selected, and a guessed combination of
            // alternatives would double count) and any other approach takes its first valued method in the picker's creation order.
            var value = ApproachValueOf(a.ApproachValue, a.ApproachType, mine);
            // A value derived from one method (nothing stored) is that method's alone: the row and the "วิธีที่ใช้" line name only it, not every
            // alternative the picker returned ("WQS + Sale Grid").
            IReadOnlyList<PickedMethod> shown = !PricingMethodPicker.HasValue(a.ApproachValue) && DerivingMethod(a.ApproachType, mine) is { } only
                ? new[] { only } : mine;
            if (!a.IsSelected && !PricingMethodPicker.HasValue(value))
                continue; // never analysed (a reference row at 0 is noise too)

            var approachLabel = PricingMethodPicker.ApproachLabel(a.ApproachType);

            rows.Add(new ApproachValueRow
            {
                GroupNumber = a.GroupNumber,
                GroupName = a.GroupName,
                ApproachType = a.ApproachType,
                ApproachLabel = approachLabel,
                Methods = shown.Select(m => new ApproachMethodRef(m.MethodType, m.Role)).ToList(),
                Value = value,
                IsReference = !a.IsSelected
            });

            if (a.IsSelected)
            {
                var marketLabels = shown.Select(m => MethodTypeLabels.GetValueOrDefault(m.MethodType)).OfType<string>().Distinct().ToList();
                var line = string.Equals(a.ApproachType, "Market", StringComparison.OrdinalIgnoreCase) && marketLabels.Count > 0
                    ? string.Join(" + ", marketLabels)
                    : approachLabel;
                if (!string.IsNullOrWhiteSpace(line) && !selectedLabels.Contains(line))
                    selectedLabels.Add(line);
            }
        }

        // pa.Remark repeats on every approach row of a group — take it once per group.
        var reasons = approaches
            .GroupBy(a => a.GroupNumber)
            .Select(g => (Group: g.Key, Remark: g.Select(a => a.Remark).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))))
            .Where(g => g.Remark is not null)
            .Select(g => multiGroup ? $"กลุ่มที่ {g.Group}: {g.Remark}" : g.Remark!)
            .ToList();

        return new PricingSummary(
            rows,
            multiGroup,
            selectedLabels.Count > 0 ? string.Join(", ", selectedLabels) : null,
            reasons.Count > 0 ? string.Join("\n", reasons) : null);
    }

    /// <summary>
    /// Builds the external cover/letter portion of <see cref="AppraisalSummaryModel"/> from an open
    /// connection. <c>IsExternal</c> is set to true; section props are left null.
    /// <paramref name="summary"/> is the bank's appraisal-summary form for this appraisal: the cover's
    /// ที่ตั้งทรัพย์สิน and the letter's พรบ.ผังเมือง are taken from it, so they are the same values the
    /// summary prints and cannot be re-composed differently here.
    /// </summary>
    internal static async Task<AppraisalSummaryModel> BuildAsync(
        IDbConnection connection,
        Guid appraisalId,
        AppraisalSummaryModel summary,
        decimal forceSaleRateDefault,
        CancellationToken cancellationToken)
    {
        // ── Batch 1: 13 result sets, single round-trip (all keyed off @AppraisalId) ──
        const string batchSql = """
            -- RS01: Q1 — Appraisal header
            SELECT
                a.AppraisalNumber,
                a.RequestId,
                a.CompletedAt
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

            -- RS03: Q3 — Latest non-cancelled External assignment
            SELECT TOP 1
                aa.AssigneeCompanyId
            FROM appraisal.AppraisalAssignments aa
            WHERE aa.AppraisalId = @AppraisalId
              AND aa.AssignmentType = 'External'
              AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
            ORDER BY aa.AssignedAt DESC, aa.Id DESC;

            -- RS04: Q5 — Appraisal date. ValuationAnalyses.ValuationDate wins; the latest
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

            -- RS05: Q6 — Property type counts
            SELECT
                ap.PropertyType,
                COUNT(*) AS PropertyCount
            FROM appraisal.AppraisalProperties ap
            WHERE ap.AppraisalId = @AppraisalId
            GROUP BY ap.PropertyType;

            -- RS06: Q13 — CollateralType code→Thai map
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'CollateralType' AND [Language] = 'TH' AND IsActive = 1;

            -- RS07: Q7 — Land rows (all land properties, ordered by sequence)
            SELECT
                lad.Id            AS LandDetailId,
                lad.ObligationDetails,
                lad.ObligationType,
                lad.MortgageDetails,
                lad.Latitude,
                lad.Longitude
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS08: Land titles (all titles across all land properties), in the same property order
            -- as RS07. Ordering by lad.Id would sort by SQL Server's uniqueidentifier byte order,
            -- which does not follow Guid.CreateVersion7 creation order — harmless while this was a
            -- flat list of numbers, but it now decides which document kind is announced first.
            SELECT
                lt.TitleNumber,
                lt.TitleType,
                lt.AreaRai,
                lt.AreaNgan,
                lt.AreaSquareWa
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber, lad.Id, lt.SequenceNumber, lt.Id;

            -- RS09: Q8 — Condo rows
            SELECT
                cad.RoomNumber      AS CondoRoomNumber,
                cad.OwnerName       AS CondoOwner,
                cad.IsOwnerVerified AS CondoOwnerVerified,
                cad.ObligationDetails AS CondoObligationDetails,
                cad.ObligationType    AS CondoObligationType,
                cad.MortgageDetails   AS CondoMortgageDetails,
                cad.Latitude        AS CondoLatitude,
                cad.Longitude       AS CondoLongitude,
                cad.UsableArea
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS10: Q10 — Machinery registration numbers
            SELECT mad.RegistrationNumber
            FROM appraisal.MachineryAppraisalDetails mad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = mad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND mad.RegistrationNumber IS NOT NULL
              AND mad.RegistrationNumber <> ''
            ORDER BY ap.SequenceNumber;

            -- RS11: Q11 — Valuation totals + block project force-sale % (the rate chain's middle step; same join as
            -- AppraisalSummaryCommonLoader RS05, so the project % still comes back before a ValuationAnalyses row exists).
            SELECT
                va.AppraisedValue,
                va.ForcedSaleValue,
                va.ForceSaleRate,
                va.InsuranceValue,
                ppa.ForceSalePercentage AS ProjectForceSalePercentage
            FROM appraisal.Appraisals ap
            LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = ap.Id
            LEFT JOIN appraisal.Projects p ON p.AppraisalId = ap.Id
            LEFT JOIN appraisal.ProjectPricingAssumptions ppa ON ppa.ProjectId = p.Id
            WHERE ap.Id = @AppraisalId AND ap.IsDeleted = 0;

            -- RS12: Q13 — Machinery surveyed count (appraisal-level, 1:1)
            SELECT mas.SurveyedNumber
            FROM appraisal.MachineryAppraisalSummaries mas
            WHERE mas.AppraisalId = @AppraisalId;

            -- RS13: Obligation code→Thai map (names of the other-obligation codes 03/04/05/99)
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'Obligation' AND [Language] = 'TH' AND IsActive = 1;
            """;

        var batchParams = new DynamicParameters();
        batchParams.Add("AppraisalId", appraisalId);

        HeaderRow? header;
        List<string> customerNames;
        AssignmentRow? assignment;
        DateTime? appraisalDate;
        List<PropertyCountRow> propCounts;
        List<ParamRow> collateralTypeParams;
        List<LandRow> landRows;
        List<TitleRow> titleRows;
        List<CondoRow> condoRows;
        List<string> machineRegs;
        ValuationRow? valuation;
        int? machineSurveyedCount;
        List<ParamRow> obligationParams;

        using (var multi = await connection.QueryMultipleAsync(batchSql, batchParams))
        {
            header = await multi.ReadFirstOrDefaultAsync<HeaderRow>();
            if (header is null)
                throw new NotFoundException("Appraisal", appraisalId.ToString());

            customerNames = (await multi.ReadAsync<string>()).ToList();
            assignment = await multi.ReadFirstOrDefaultAsync<AssignmentRow>();
            appraisalDate = await multi.ReadFirstOrDefaultAsync<DateTime?>();
            propCounts = (await multi.ReadAsync<PropertyCountRow>()).ToList();
            collateralTypeParams = (await multi.ReadAsync<ParamRow>()).ToList();
            landRows = (await multi.ReadAsync<LandRow>()).ToList();
            titleRows = (await multi.ReadAsync<TitleRow>()).ToList();
            condoRows = (await multi.ReadAsync<CondoRow>()).ToList();
            machineRegs = (await multi.ReadAsync<string>()).ToList();
            valuation = await multi.ReadFirstOrDefaultAsync<ValuationRow>();
            machineSurveyedCount = await multi.ReadFirstOrDefaultAsync<int?>();
            obligationParams = (await multi.ReadAsync<ParamRow>()).ToList();
        }

        var customerName = customerNames.Count > 0
            ? string.Join(" และ ", customerNames)
            : null;

        // ── Company details (conditional on a valid AssigneeCompanyId Guid) ──────────
        string? companyName = null;
        string? companyAddress = null;
        string? companyTel = null;

        if (assignment is not null
            && !string.IsNullOrWhiteSpace(assignment.AssigneeCompanyId)
            && Guid.TryParse(assignment.AssigneeCompanyId, out var companyGuid))
        {
            // The appraisal book is a Thai-language document, so prefer the Thai company name and
            // fall back to the English one only when the company has none.
            const string companySql = """
                SELECT
                    COALESCE(NULLIF(c.NameLocal, N''), c.Name) AS Name,
                    c.Phone,
                    c.AddressLine1,
                    c.AddressLine2
                FROM auth.Companies c
                WHERE c.Id = @CompanyId
                """;

            var companyParams = new DynamicParameters();
            companyParams.Add("CompanyId", companyGuid);

            var company = await connection.QueryFirstOrDefaultAsync<CompanyRow>(companySql, companyParams);
            if (company is not null)
            {
                companyName = company.Name;
                companyTel = company.Phone;
                var addrParts = new List<string?> { company.AddressLine1, company.AddressLine2 }
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                companyAddress = addrParts.Count > 0 ? string.Join(" ", addrParts) : null;
            }
        }

        // ── Compose derived fields ───────────────────────────────────────────────────
        var collateralTypeMap = collateralTypeParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);

        // GPS: prefer land → condo, in the same "N 13.000000 E 100.000000" form as everywhere else (ThaiAddressFormatter.FormatGps; null unless both coordinates exist).
        var firstLand = landRows.FirstOrDefault();
        var firstCondo = condoRows.FirstOrDefault();
        string? gps = ThaiAddressFormatter.FormatGps(firstLand?.Latitude, firstLand?.Longitude)
                      ?? ThaiAddressFormatter.FormatGps(firstCondo?.CondoLatitude, firstCondo?.CondoLongitude);

        // Title deed numbers and land area. The numbers are grouped under the name of the document
        // that carries them ("โฉนดที่ดินเลขที่ 1234, 1235 / น.ส.3 เลขที่ 5678") — the row used to
        // print bare numbers under a heading that assumed they were all title deeds (CA-609).
        // Groups keep the order their first parcel appeared in, which RS08 orders by property
        // sequence, so the kinds read in the same order as the property list above.
        var deeds = SummariseTitleDeeds(titleRows.Select(t => (t.TitleType, t.TitleNumber)));
        string? titleDeedNumbers = deeds.Text;
        int? totalTitleDeeds = deeds.Total > 0 ? deeds.Total : null;

        // The letter: show the area line whenever any title exists (preserves the original output, including a "0 - 0 - 0" line for titles with no captured
        // area); hide it only when there are no titles. The summary page prints the same area in words (ThaiLandAreaFormatter.Describe), nothing when it is 0.
        string? landAreaText = titleRows.Count > 0
            ? ThaiLandAreaFormatter.FormatTotal(
                titleRows.Sum(t => t.AreaRai ?? 0m),
                titleRows.Sum(t => t.AreaNgan ?? 0m),
                titleRows.Sum(t => t.AreaSquareWa ?? 0m))
            : null;
        var deedSquareWa = titleRows.Sum(t => (t.AreaRai ?? 0m) * 400m + (t.AreaNgan ?? 0m) * 100m + (t.AreaSquareWa ?? 0m));
        string? landAreaWords = deedSquareWa > 0m ? ThaiLandAreaFormatter.Describe(deedSquareWa) : null;

        // Owners. The land and building owners and the อาคารสิ่งปลูกสร้าง row are built by AppraisalBookDataProvider from the loaded
        // sections (LetterOwnerRows), so their numbering matches the book; the condo owners come from these rows.
        string RoomLabel(int i) => ThaiAddressFormatter.RoomLabel(condoRows[i].CondoRoomNumber, i);
        // The condo owner row is worded later by LetterOwnerRows.CondoOwners, once the book's body type (which decides its pointer) is known.

        // Machinery registration numbers: one entry per number, runs of consecutive numbers collapsed.
        var machineRegistrations = MachineRegistrationList.Build(machineRegs);

        // Obligations: one item per condo room here; the lands' come from AppraisalBookDataProvider's loaded land sections, and LetterOwnerRows.Obligations
        // combines both (so a land + condo book keeps the condo unit's mortgage) under the same "ไม่มี" rule. Machinery only prints nothing.
        var obligationNames = obligationParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);
        var condoObligations = condoRows.Select((c, i) => new ExternalObligationItem
        {
            Label = RoomLabel(i),
            // An unverified owner reads "ไม่สามารถตรวจสอบได้" before the rooms are grouped, so it counts like any other text.
            Owner = AppraisalSummaryCommonLoader.OwnerDisplay(c.CondoOwner, c.CondoOwnerVerified),
            Mortgage = ObligationText.Mortgage(c.CondoObligationType, c.CondoMortgageDetails),
            Others = ObligationText.Others(c.CondoObligationType, c.CondoObligationDetails, obligationNames)
        }).ToList();

        // Property-type summary
        var condoUsableAreaTotal = condoRows.Sum(c => c.UsableArea ?? 0m);
        var landSqWaTotal = titleRows.Sum(t => t.AreaSquareWa ?? 0m);

        var summarySections = new List<string>();
        foreach (var pc in propCounts.OrderBy(p => p.PropertyType))
        {
            // PropertyType stores domain family codes — translate via representative CollateralType code.
            var typeThai = CollateralFamilyTranslator.ToThai(pc.PropertyType, collateralTypeMap) ?? "";

            string? section;
            if (CollateralFamilyTranslator.IsCondoFamily(pc.PropertyType))
            {
                section = condoUsableAreaTotal > 0
                    ? $"{typeThai} จำนวน {pc.PropertyCount} ยูนิต เนื้อที่ {condoUsableAreaTotal:#,##0.##} ตารางเมตร"
                    : $"{typeThai} จำนวน {pc.PropertyCount} ยูนิต";
            }
            else if (CollateralFamilyTranslator.IsEquipmentFamily(pc.PropertyType))
            {
                // A stored SurveyedNumber of 0 is the default/unset value, not a real count —
                // omit the "surveyed N" clause rather than print "สำรวจพบ 0 เครื่อง". Matches the
                // sibling AppraisalSummaryMachineDataProvider, which also treats 0 as "not entered".
                section = machineSurveyedCount is int surveyed && surveyed > 0
                    ? $"{typeThai} จำนวน {pc.PropertyCount} เครื่อง สำรวจพบ {surveyed} เครื่อง"
                    : $"{typeThai} จำนวน {pc.PropertyCount} เครื่อง";
            }
            else
            {
                section = landSqWaTotal > 0
                    ? $"{typeThai} จำนวน {pc.PropertyCount} หลัง เนื้อที่ {landSqWaTotal:#,##0.##} ตารางวา"
                    : $"{typeThai} จำนวน {pc.PropertyCount} หลัง";
            }
            summarySections.Add(section);
        }

        string? propertyTypeSummary = summarySections.Count > 0
            ? string.Join(", ", summarySections)
            : null;

        // ── Pricing: approach value table, selection reason, method line ─────────────
        var pricing = await LoadPricingAsync(connection, appraisalId, cancellationToken);

        // Verify date: CompletedAt (external verify) or appraisalDate fallback
        DateTime? verifyDate = header.CompletedAt ?? appraisalDate;

        // ── Assemble model (cover/letter fields only; sections loaded by the provider) ──
        return new AppraisalSummaryModel
        {
            IsExternal = true,
            BankName = BankName,
            Subject = LetterSubject,
            AppraisalPurpose = ExternalAppraisalPurpose,
            AppraisalCriteria = ExternalAppraisalCriteria,

            CompanyName = companyName,
            CompanyAddress = companyAddress,
            CompanyTel = companyTel,
            // Field 11 — on an external book the appraising entity IS the assignee company
            // (the internal path resolves this to the bank in AppraisalSummaryCommonLoader).
            // Consumed by the machine section's §1.3 survey line.
            Appraiser = companyName,
            CustomerName = customerName,
            AppraisalBookNumber = header.AppraisalNumber,
            PropertyTypeSummary = propertyTypeSummary,
            CollateralLocation = summary.CollateralAddress,
            ProjectAddress = summary.ProjectAddress,
            CityPlan = string.IsNullOrWhiteSpace(summary.CityPlan) ? null : summary.CityPlan,
            VerifyDate = verifyDate,
            Gps = gps,
            TitleDeedNumbers = titleDeedNumbers,
            TotalTitleDeeds = totalTitleDeeds,
            TitleDeedsSummarised = deeds.Summarised,
            UnnumberedTitleDeeds = deeds.Unnumbered,
            LandAreaText = landAreaText,
            LandAreaWords = landAreaWords,
            TitleDeedGroups = deeds.GroupedText,
            MachineRegistrations = machineRegistrations,
            CondoObligationItems = condoObligations,
            PriceMethod = pricing.PriceMethod,
            ApproachValues = pricing.Rows,
            ApproachValuesMultiGroup = pricing.MultiGroup,
            SelectionReason = pricing.Reason,
            AppraisalDate = appraisalDate,
            CollateralValue = valuation?.AppraisedValue,
            ForcedSaleValue = valuation?.ForcedSaleValue,
            ForceSaleRate = BookForceSaleRate.Resolve(valuation?.ForceSaleRate, valuation?.ProjectForceSalePercentage, forceSaleRateDefault),
            BuildingCoverageAmount = valuation?.InsuranceValue,
        };
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class ApproachRow
    {
        public int GroupNumber { get; init; }
        public string? GroupName { get; init; }
        public Guid ApproachId { get; init; }
        public string? ApproachType { get; init; }
        public bool IsSelected { get; init; }
        public decimal? ApproachValue { get; init; }
        public string? Remark { get; init; }
    }

    /// <summary>The letter's เอกสารสิทธิ์ที่ดิน row: the text, the deed count, and whether the text is a per-type summary.</summary>
    internal sealed record TitleDeedSummary(string? Text, int Total, bool Summarised, int Unnumbered, string? GroupedText = null);

    /// <summary>Above this many numbered deeds the letter prints a count per document type instead of the numbers.</summary>
    private const int MaxListedDeeds = 10;

    /// <summary>
    /// Numbers are grouped under the name of the document that carries them ("โฉนดที่ดินเลขที่ 1234, 1235 / น.ส.3 เลขที่ 5678" — CA-609),
    /// in the order each kind first appeared (RS08 orders by property sequence). A deed is a distinct (type, number); a row with no number
    /// (blank or "-") is counted in <c>Total</c> and in <c>Unnumbered</c> but cannot be listed, so the count and the list always agree.
    /// With more than <see cref="MaxListedDeeds"/> numbered deeds the letter's text is "โฉนดที่ดิน จำนวน 48 ฉบับ / น.ส.3 ก. จำนวน 2 ฉบับ"
    /// and the template adds where the deeds are listed.
    /// </summary>
    internal static TitleDeedSummary SummariseTitleDeeds(IEnumerable<(string? TitleType, string? TitleNumber)> rows)
    {
        var byKind = new List<(string Prefix, string Noun, List<string> Numbers)>();
        var unnumbered = 0;
        foreach (var (type, rawNumber) in rows)
        {
            if (ThaiAddressFormatter.Stated(rawNumber) is not { } number) { unnumbered++; continue; }

            var prefix = TitleDeedLabel.NumberPrefix(type);
            var kind = byKind.FindIndex(k => k.Prefix == prefix);
            if (kind < 0) { byKind.Add((prefix, TitleDeedLabel.Noun(type), [])); kind = byKind.Count - 1; }
            if (!byKind[kind].Numbers.Contains(number)) byKind[kind].Numbers.Add(number);
        }

        var numbered = byKind.Sum(k => k.Numbers.Count);
        if (numbered == 0)
            return new TitleDeedSummary(null, unnumbered, false, unnumbered, null);

        var summarised = numbered > MaxListedDeeds;
        // The company letter's row: the numbers grouped under the name of the document that carries them, "โฉนดที่ดินเลขที่ 1, 2, 3 / น.ส.3 เลขที่ 5"
        // (CA-609); above 10 deeds a count per type, "โฉนดที่ดิน จำนวน 48 ฉบับ / น.ส.3 ก. จำนวน 2 ฉบับ".
        var text = string.Join(" / ", byKind.Select(k => summarised
            ? $"{k.Noun} จำนวน {k.Numbers.Count} ฉบับ"
            : $"{k.Prefix} {string.Join(", ", k.Numbers)}"));
        // The summary page's ที่ดิน line reads the same deeds as running Thai text: "โฉนดที่ดินเลขที่ 101, 102 และ 103 และ น.ส.3 ก. เลขที่ 204"; above 10 deeds
        // "โฉนดที่ดิน จำนวน 40 ฉบับ และ น.ส.3 ก. จำนวน 5 ฉบับ".
        var grouped = string.Join(" และ ", byKind.Select(k => summarised
            ? $"{k.Noun} จำนวน {k.Numbers.Count} ฉบับ"
            : $"{k.Prefix} {ThaiTextList.Join(k.Numbers)}"));
        return new TitleDeedSummary(text, numbered + unnumbered, summarised, unnumbered, grouped);
    }

    private sealed class HeaderRow
    {
        public string? AppraisalNumber { get; init; }
        public Guid RequestId { get; init; }
        public DateTime? CompletedAt { get; init; }
    }

    private sealed class AssignmentRow
    {
        public string? AssigneeCompanyId { get; init; }
    }

    private sealed class CompanyRow
    {
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
    }

    private sealed class PropertyCountRow
    {
        public string? PropertyType { get; init; }
        public int PropertyCount { get; init; }
    }

    private sealed class LandRow
    {
        public Guid LandDetailId { get; init; }
        public string? ObligationDetails { get; init; }
        public string? ObligationType { get; init; }
        public string? MortgageDetails { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
    }

    private sealed class TitleRow
    {
        public string? TitleNumber { get; init; }
        public string? TitleType { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
    }

    private sealed class CondoRow
    {
        public string? CondoOwner { get; init; }
        public bool? CondoOwnerVerified { get; init; }
        public string? CondoRoomNumber { get; init; }
        public string? CondoObligationDetails { get; init; }
        public string? CondoObligationType { get; init; }
        public string? CondoMortgageDetails { get; init; }
        public decimal? CondoLatitude { get; init; }
        public decimal? CondoLongitude { get; init; }
        public decimal? UsableArea { get; init; }
    }

    private sealed class ValuationRow
    {
        public decimal? AppraisedValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? ForceSaleRate { get; init; }
        public decimal? InsuranceValue { get; init; }
        public decimal? ProjectForceSalePercentage { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
    }
}
