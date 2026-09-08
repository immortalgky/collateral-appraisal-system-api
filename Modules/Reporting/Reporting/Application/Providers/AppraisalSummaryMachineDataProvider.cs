using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;
using Shared.Configuration;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.3.3
/// "ใบสรุปรายงานการประเมิน – เครื่องจักร".
///
/// Common queries (Q1–Q14 + ColTypeMap) are delegated to
/// <see cref="AppraisalSummaryCommonLoader"/> (itself batched in Phase C).
///
/// Phase C — this provider batches its own 2 machine-specific queries into one
/// QueryMultiple call (single round-trip):
///   RS01  QM1  appraisal.MachineryAppraisalSummaries — appraisal-level summary
///   RS02  QM2  appraisal.PropertyGroupItems + MachineryAppraisalDetails — per-group detail
///
/// Column notes:
///   - MachineryAppraisalSummaries uses schema=appraisal (DbContext default schema).
///   - MachineAge is a decimal? on the entity; the config does not call HasColumnName,
///     so the DB column is "MachineAge".
///   - YearOfManufacture and Quantity are int? on the entity; no HasColumnName override.
///   - The FSD §2.1.3.3 "ประเภทเครื่องจักร" field maps to InIndustrial (industry category).
///   - "สภาพความต้องการของตลาด" maps to MachineryAppraisalSummary.MarketDemand.
///   - Per FSD: พื้นที่/จำนวน and ราคาต่อหน่วย columns are BLANK for machine groups.
///     A subtotal row "รวมมูลค่าเครื่องจักร" is added after all group rows.
/// </summary>
public sealed class AppraisalSummaryMachineDataProvider(
    ISqlConnectionFactory connectionFactory,
    ISystemConfigurationReader configReader,
    ILogger<AppraisalSummaryMachineDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-machine";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        var forceSaleRateDefault = await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", 70m, cancellationToken);

        // ── Common data (Q1–Q14 + ColTypeMap) ───────────────────────────────────
        var common = await AppraisalSummaryCommonLoader.LoadAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);
        if (common is null)
            throw new NotFoundException("Appraisal", entityId);

        // ── Batch: 2 machine-specific result sets, single round-trip ─────────────
        const string batchSql = """
            -- RS01: QM1 — Machinery appraisal summary (appraisal-level)
            SELECT
                mas.InIndustrial,
                mas.MarketDemand,
                mas.Proprietor,
                mas.Owner,
                mas.MachineAddress,
                mas.Latitude,
                mas.Longitude,
                mas.Obligation,
                mas.SurveyedNumber
            FROM appraisal.MachineryAppraisalSummaries mas
            WHERE mas.AppraisalId = @AppraisalId;

            -- RS02: QM2 — Per-group machine detail rows
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                -- One name, two columns. The appraisal form now writes only PropertyName; rows
                -- created before that carry the same text in MachineName, so fall back to it
                -- rather than printing a nameless item. Same order the pricing analysis uses.
                COALESCE(NULLIF(mad.PropertyName, ''), NULLIF(mad.MachineName, '')) AS MachineName,
                mad.RegistrationNumber,
                mad.Brand,
                mad.Model,
                mad.Series,
                mad.EngineNo,
                mad.ChassisNo,
                mad.SerialNo,
                mad.Manufacturer,
                mad.YearOfManufacture,
                mad.MachineAge,
                mad.ReplacementValue,
                mad.ConditionValue,
                mad.MachineCondition,
                mad.OwnerName,
                mad.Location,
                mad.Quantity,
                mad.RegistrationStatus,
                mad.InstallationStatus,
                mad.IsPriceCertified,
                mad.ConditionUse,
                mv.FairMarketValue AS AppraisedValue
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
            -- NOTE on reconciliation: this is Σ FairMarketValue, while the group's own figure on
            -- the subtotal row is Q9's COALESCE(FinalAppraisedValue, FinalValueRounded,
            -- AppraisalPrice). They agree on every live path — MirrorMachineCostTotalToFinalValue
            -- writes Σ FMV into both FinalValue and FinalValueRounded with no rounding. They would
            -- diverge if someone overrode a MachineryCost method's final value through the
            -- type-agnostic UpdateFinalValue endpoint, but nothing calls it: useSetFinalValue and
            -- useUpdateFinalValue have zero call sites in the app.
            -- Per-machine value for the "สรุปมูลค่าเครื่องจักร" line under each set. It lives on
            -- the cost method, not on the machine, so it is read through the SAME selected
            -- approach + method the group's own figure comes from (see Q9 in
            -- AppraisalSummaryCommonLoader). Reading an unselected method instead would print a
            -- number the appraiser rejected under a group subtotal that stayed blank.
            -- OUTER APPLY, not a JOIN: it returns at most one row, so this can never fan the
            -- machine list out even if a property ever carries two cost items.
            OUTER APPLY (
                SELECT TOP 1 mci.FairMarketValue
                FROM appraisal.MachineCostItems mci
                JOIN appraisal.PricingAnalysisMethods pam
                    ON pam.Id = mci.PricingMethodId
                   AND pam.MethodType = 'MachineryCost'
                   AND pam.IsSelected = 1
                JOIN appraisal.PricingAnalysisApproaches pap
                    ON pap.Id = pam.ApproachId
                   AND pap.IsSelected = 1
                -- Anchored to THIS group's own analysis: the same two conditions Q9 applies when it
                -- resolves the group's figure. Without them a machine costed under another group's
                -- analysis could contribute a value that group's subtotal never counted.
                JOIN appraisal.PricingAnalysis pa
                    ON pa.Id = pap.PricingAnalysisId
                   AND pa.AnchorId = pgi.PropertyGroupId
                   AND pa.SubjectType = 0
                WHERE mci.AppraisalPropertyId = ap.Id
                -- The unique index is (PricingMethodId, AppraisalPropertyId) — per METHOD, not per
                -- property — so nothing stops two selected methods holding an item for the same
                -- machine. ORDER BY is what keeps TOP 1 from picking a different one each render.
                ORDER BY mci.Id
            ) mv
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup;
            """;

        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        MachSummaryRow? machSummary;
        List<GroupMachineDetailRow> groupMachineRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01
            machSummary = await multi.ReadFirstOrDefaultAsync<MachSummaryRow>();

            // RS02
            groupMachineRows = (await multi.ReadAsync<GroupMachineDetailRow>()).ToList();
        }

        // GPS from the machinery summary table
        var gps = ThaiAddressFormatter.FormatGps(machSummary?.Latitude, machSummary?.Longitude);

        var collateralAddress = string.IsNullOrWhiteSpace(machSummary?.MachineAddress)
            ? null
            : machSummary.MachineAddress.Trim();

        var machineByGroup = groupMachineRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SequenceInGroup).ToList());

        // ── Build per-group summary rows (machine / vehicle / vessel groups only) ──
        var machineFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MAC", "VEH", "VES" };
        var machineGroupList = common.GroupRows
            .Where(g => g.PropertyType != null && machineFamily.Contains(g.PropertyType))
            .ToList();

        // SurveyedNumber is appraisal-level ("global for all machines"). It is only a valid
        // per-group count when there is a single machine group; with multiple groups it would
        // print the appraisal-wide total on every group, so we fall back to per-group counts.
        var singleMachineGroup = machineGroupList.Count == 1;

        var summaryGroups = machineGroupList
            .Select(g =>
        {
            machineByGroup.TryGetValue(g.GroupId, out var machRows);
            machRows ??= [];

            // Per-group count: detail rows if present, else the group's property count.
            // For a single machine group, prefer the appraiser-entered surveyed count — but only
            // when it is a positive value; a stored 0 (default/unset) means "not entered" and must
            // fall back to the per-group count rather than hiding the header line.
            int perGroupCount = machRows.Count > 0 ? machRows.Count : g.PropertyCount;
            int machineCount = singleMachineGroup && machSummary?.SurveyedNumber is int surveyed && surveyed > 0
                ? surveyed
                : perGroupCount;
            // Header line(s) above the numbered item list. A group can mix registration states
            // (the sample reports show a registered batch and an unregistered one in the same
            // appraisal), so emit one line per state that is actually present rather than the
            // blanket "จดทะเบียนกรรมสิทธิ์" claim this used to print for every machine.
            var sections = BuildMachineSections(machRows);

            // Fallback for the pre-migration case where no detail rows were loaded: keep the old
            // single-line header driven by the count so the row never renders empty.
            var collateralDetails = sections.Count == 0 && machineCount > 0
                ? $"จดทะเบียนกรรมสิทธิ์เครื่องจักร จำนวน {machineCount} รายการ"
                : null;


            return new SummaryGroupRow
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                // Resolved here so the template prints a name without a null check. The domain
                // requires every group to have one, so the numbered form is defensive only.
                GroupLabel = string.IsNullOrWhiteSpace(g.GroupName)
                    ? $"กลุ่มที่ {g.GroupNumber}"
                    : g.GroupName,
                PropertyType = "เครื่องจักร",
                CollateralDetails = collateralDetails,
                MachineSections = sections.Count > 0 ? sections : null,
                DetailItems = [],
                AreaOrUnit = null,
                PricePerAreaOrUnit = null,
                AppraisalValue = g.GroupAppraisalValue,
                Condition = null,
                Remark = null
            };
        }).ToList();

        // วิธีการประเมิน — scoped to the methods of the machine groups actually shown.
        var methodFlags = AppraisalSummaryCommonLoader.FlagsForGroups(
            common.GroupMethodTypes,
            machineGroupList.Select(g => g.GroupId));

        // ทุนประกันภัยเครื่องจักร is the machinery appraisal value, so the totals block prints the
        // same figure twice. It deliberately does NOT use common.BuildingCoverageAmount
        // (ValuationAnalyses.InsuranceValue): BuildingInsuranceCalculator sums buildings and condos
        // only, so a machinery-only appraisal gets 0, and on a mixed appraisal that column carries
        // the BUILDING coverage — which has no business appearing on the machine form.
        //
        // Null (the totals row prints "-") when not one machine group carries a figure, and NOT the
        // Count > 0 fallback TotalAppraisalValue keeps below. Two reasons: common.TotalAppraisalValue
        // is appraisal-wide, so on a mixed appraisal it would state land and buildings as machinery
        // insurance capital — the very thing this field must not do; and a bare Sum() over unpriced
        // groups returns 0m, which Scriban prints as "0.00", asserting "insured for nothing" where
        // the truth is "not priced yet".
        decimal? machineAppraisalValue = summaryGroups.Any(g => g.AppraisalValue.HasValue)
            ? summaryGroups.Sum(g => g.AppraisalValue ?? 0m)
            : null;

        // ราคาบังคับขาย is the machines' own too, for the same reason the two rows above it are:
        // common.ForcedSaleValue is appraisal-wide, so on a mixed appraisal this form printed a
        // forced-sale figure LARGER than the total it sits under.
        //
        // Scaled by the machine share of the appraisal rather than re-applying the force-sale rate,
        // so this form and the land/building form of the same appraisal quote ONE rate even when the
        // appraiser has overridden ValuationAnalyses.ForcedSaleValue by hand. Same treatment
        // AppraisalSummaryLandBuildingDataProvider gives its ตามสภาพปัจจุบัน split.
        //
        // Both operands are present whenever machineAppraisalValue is: common.TotalAppraisalValue
        // falls back to Σ over ALL groups, and common.ForcedSaleValue to that total × the rate, so a
        // priced machine group guarantees both. The guards are there for the case where no machine
        // group carries a figure — then this row prints "-" rather than the appraisal-wide number,
        // matching what the two rows above it do.
        decimal? machineForcedSaleValue =
            machineAppraisalValue is { } machineTotal
            && common.ForcedSaleValue is { } appraisalForcedSale
            && common.TotalAppraisalValue is { } appraisalTotal
            && appraisalTotal != 0m
                ? Math.Round(appraisalForcedSale * machineTotal / appraisalTotal, 2, MidpointRounding.AwayFromZero)
                : null;

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = common.AppraisalNumber,
            AppraisalDate = common.AppraisalDate,
            CustomerName = common.CustomerName,
            AoName = common.AoName,
            AppraisalPurpose = common.AppraisalPurpose,
            // Machine form: property type is fixed (header + appraiser opinion).
            PropertyType = "เครื่องจักร",
            SummaryPropertyType = "เครื่องจักร",
            // ที่ตั้งทรัพย์สิน from the Request detail (same as land-building); fall back to the machine's own address.
            CollateralAddress = common.CollateralAddress ?? collateralAddress,
            AdministrativeDistrict = common.AdministrativeDistrict,
            LandOffice = null,
            OldAppraisalValue = common.PrevAppraisedValue,
            HasPrevAppraisal = common.HasPrevAppraisal,
            IsReAppraisal = string.Equals(common.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase),
            // The info row prints "ขอเพิ่มวงเงิน" instead of "วงเงินสินเชื่อ" on an increase-limit
            // appraisal, the same as the land/building and condo summaries — which needs this set,
            // and the figure it is being compared against carried alongside it.
            IsIncreaseLimit = common.IsIncreaseLimit,
            ExistingLoanValue = common.ExistingLoanValue,
            Appraiser = common.Appraiser,
            LoanValue = common.LoanValue,
            Groups = summaryGroups,
            TotalAppraisalValue = summaryGroups.Count > 0 ? machineAppraisalValue ?? 0m : common.TotalAppraisalValue,
            BuildingCoverageAmount = machineAppraisalValue,
            ForcedSaleValue = machineForcedSaleValue,
            Condition = common.Condition,
            Remark = common.Remark,
            // กรรมสิทธิ์เครื่องจักร = registered title holder. Prefer the appraisal-level
            // Proprietor (ผู้ถือกรรมสิทธิ์) captured on the machinery summary; fall back to the
            // possessor (Owner) then a per-machine owner name.
            LandOwner = FirstNonBlank(
                machSummary?.Proprietor,
                machSummary?.Owner,
                groupMachineRows.Select(r => r.OwnerName).FirstOrDefault(o => !string.IsNullOrWhiteSpace(o))),
            EntryExitRights = null,
            BuildingOwner = null,
            LandCondition = null,
            Obligation = string.IsNullOrWhiteSpace(machSummary?.Obligation)
                ? AppraisalSummaryModel.NoObligationText
                : machSummary!.Obligation,
            CityPlan = null,
            Gps = gps,
            GovernmentAssessedValue = null,
            Utilization = null,
            MachineType = machSummary?.InIndustrial,
            MarketDemandConditions = machSummary?.MarketDemand,
            IsWqs = methodFlags.IsWqs,
            IsSaleGrid = methodFlags.IsSaleGrid,
            IsCost = methodFlags.IsCost,
            IsIncome = methodFlags.IsIncome,
            IsHypothesis = methodFlags.IsHypothesis,
            IsLeasehold = methodFlags.IsLeasehold,
            IsProfitRent = methodFlags.IsProfitRent,
            AppraiserComment = common.AppraiserComment,
            AppraisalStaffName = common.StaffName,
            AppraisalStaffPosition = common.StaffPosition,
            AppraisalCheckerName = common.CheckerName,
            AppraisalCheckerPosition = common.CheckerPosition,
            AppraisalVerifyName = common.VerifyName,
            AppraisalVerifyPosition = common.VerifyPosition,
            MeetingNumber = common.Review?.MeetingNo,
            MeetingDate = common.Review?.MeetingDate,
            ApprovalDate = common.ApprovalDate,
            IsCompleted = common.IsCompleted,
            ShowMeeting = common.ShowMeeting,
            ApproverDecisionApproved = common.ApproverDecisionApproved,
            Approvers = common.Approvers,
            ApproverSummaryComment = common.CommitteeOpinion
        };

        logger.LogDebug(
            "AppraisalSummaryMachine model assembled for appraisal {AppraisalId}: " +
            "{GroupCount} groups, {ApproverCount} approvers, showMeeting={ShowMeeting}",
            appraisalId, summaryGroups.Count, common.Approvers.Count, common.ShowMeeting);

        return model;
    }

    /// <summary>Returns the first argument that is not null/whitespace, or null.</summary>
    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// MachineStatus parameter code for "อยู่ระหว่างการจัดซื้อ" (valued from a quotation). The
    /// report calls this set "ยังไม่ติดตั้ง", which is the same machines said from the reader's
    /// side: nothing bought from a quotation has been installed yet.
    /// </summary>
    private const string UnderProcurementStatus = "2";

    /// <summary>ConditionUse parameter code for "not found" on the survey.</summary>
    private const string NotFoundCondition = "03";

    /// <summary>
    /// Splits a group's machines into the sets the form prints, one section per set, in report
    /// order. A set with no machines produces nothing, so a group of one kind still reads as one
    /// section — which is how the majority of appraisals print.
    /// </summary>
    private static List<SummaryMachineSection> BuildMachineSections(List<GroupMachineDetailRow> rows)
    {
        // Every machine matches exactly one bucket: "ยังไม่ติดตั้ง" is decided by the installation
        // status ALONE and takes precedence, so a registered machine still under procurement is
        // reported as not installed rather than as registered. The first two buckets therefore
        // exclude it explicitly — nothing is dropped and nothing is counted twice.
        //
        // Counts are ROWS, matching the numbered list underneath, so a reader can check a heading
        // against the items below it. (Quantity is deliberately not summed here: the cost-approach
        // section reports that separately as สำรวจพบ, and mixing the two units in one cell would
        // make the heading disagree with the list.)
        var buckets = new (string Heading, Func<GroupMachineDetailRow, bool> Match)[]
        {
            ("เครื่องจักรและอุปกรณ์ที่ได้จดทะเบียนกรรมสิทธิ์",
                r => r.RegistrationStatus && r.InstallationStatus != UnderProcurementStatus),
            ("เครื่องจักรและอุปกรณ์ที่ยังไม่ได้รับการจดทะเบียน",
                r => !r.RegistrationStatus && r.InstallationStatus != UnderProcurementStatus),
            ("เครื่องจักรและอุปกรณ์ที่ยังไม่ติดตั้ง",
                r => r.InstallationStatus == UnderProcurementStatus),
        };

        var sections = new List<SummaryMachineSection>();

        foreach (var (heading, match) in buckets)
        {
            // rows arrive ordered by SequenceInGroup and Where keeps that order, so the machines
            // stay in the sequence the appraiser gave them.
            var members = rows.Where(match).ToList();
            if (members.Count == 0)
                continue;

            // Described first: a machine with nothing recorded on it prints no line, and a heading
            // that counted it would claim more than the list beneath it shows.
            var described = members.Select(DescribeMachine).Where(t => t.Length > 0).ToList();
            // Not one machine in the set could be described — no name, brand, model, registration,
            // serial, year or condition on any of them. Drop the set entirely rather than print a
            // heading reading "จำนวน 0 เครื่อง" over nothing. If such a machine also carried a
            // price its money leaves this cell with it, and the group's own figure (which counts
            // every cost item) will exceed the sets above it. That is the lesser of the two: the
            // money still reaches the page on the group subtotal or the grand total, whereas the
            // alternative prints a set that names nothing and counts no one. Not reachable on dev
            // — all six machines this blank are unpriced.
            if (described.Count == 0)
                continue;

            // The money is summed over EVERY machine in the set, including any the line above
            // dropped — deliberately a different denominator from จำนวน N เครื่อง.
            // The two answer to different things. The count answers to the list printed under it,
            // which a reader can check. The total answers to the group's subtotal row, which is the
            // method's sum over all its cost items: leave a priced-but-nameless machine out and the
            // sections visibly fail to add up to the row printed directly beneath them, on the same
            // page. Nothing prints a per-machine price, so no reader can catch the total counting a
            // machine the list does not show — but everyone can catch the columns not adding up.
            // A set where not one machine has been priced gets no total at all: summing to 0.00
            // would read as "these are worth nothing" rather than "nobody has priced these yet".
            decimal? total = members.Any(m => m.AppraisedValue.HasValue)
                ? members.Sum(m => m.AppraisedValue ?? 0m)
                : null;

            sections.Add(new SummaryMachineSection
            {
                Heading = $"{heading} จำนวน {described.Count} เครื่อง",
                Items = described,
                TotalValue = total
            });
        }

        return sections;
    }

    /// <summary>One machine as a single sentence, in the order the sample reports read.</summary>
    private static string DescribeMachine(GroupMachineDetailRow m)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(m.MachineName))
            parts.Add(m.MachineName);
        if (!string.IsNullOrWhiteSpace(m.Brand))
            parts.Add($"ยี่ห้อ {m.Brand}");
        if (!string.IsNullOrWhiteSpace(m.Model))
            parts.Add($"รุ่น {m.Model}");
        if (!string.IsNullOrWhiteSpace(m.RegistrationNumber))
            parts.Add($"ทะเบียนเลขที่ {m.RegistrationNumber}");
        if (!string.IsNullOrWhiteSpace(m.SerialNo))
            parts.Add($"หมายเลขเครื่อง {m.SerialNo}");
        if (m.YearOfManufacture.HasValue)
            parts.Add($"ปีที่ผลิต {m.YearOfManufacture}");
        if (!string.IsNullOrWhiteSpace(m.MachineCondition))
            parts.Add($"สภาพ{m.MachineCondition}");

        // The appraiser records "surveyed but missing" on ConditionUse; the reports call it out
        // per item rather than dropping the machine from the list.
        if (m.ConditionUse == NotFoundCondition)
            parts.Add("(สำรวจไม่พบ)");

        // IsPriceCertified is the "รับรองราคาประเมิน" toggle on the machine form; the report says
        // the negative of that same label so the paper and the screen use one vocabulary.
        // Certifying a price and appraising a value are one decision here, not two, so there is
        // only ever one phrase to print.
        if (!m.IsPriceCertified)
            parts.Add("(ไม่รับรองราคา)");

        return string.Join(" ", parts);
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class MachSummaryRow
    {
        public string? InIndustrial { get; init; }
        public string? MarketDemand { get; init; }
        public string? Proprietor { get; init; }
        public string? Owner { get; init; }
        public string? MachineAddress { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? Obligation { get; init; }
        public int? SurveyedNumber { get; init; }
    }

    private sealed class GroupMachineDetailRow
    {
        public Guid PropertyGroupId { get; init; }
        public int SequenceInGroup { get; init; }
        public string? MachineName { get; init; }
        public string? RegistrationNumber { get; init; }
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public string? Series { get; init; }
        public string? EngineNo { get; init; }
        public string? ChassisNo { get; init; }
        public string? SerialNo { get; init; }
        public string? Manufacturer { get; init; }
        public int? YearOfManufacture { get; init; }
        public decimal? MachineAge { get; init; }
        public decimal? ReplacementValue { get; init; }
        public decimal? ConditionValue { get; init; }
        public string? MachineCondition { get; init; }
        public string? OwnerName { get; init; }
        public string? Location { get; init; }
        public int? Quantity { get; init; }
        public bool RegistrationStatus { get; init; }
        public string? InstallationStatus { get; init; }
        public bool IsPriceCertified { get; init; }
        public string? ConditionUse { get; init; }
        public decimal? AppraisedValue { get; init; }
    }
}
